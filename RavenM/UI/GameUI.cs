using HarmonyLib;
using Steamworks;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RavenM.UI
{
    [HarmonyPatch(typeof(IngameUI),"Awake")]
    public class IngameUIPatch
    {
       static void Postfix()
       {
            IngameUI.instance.gameObject.AddComponent<GameUI>();
       }
    }

    [HarmonyPatch(typeof(ScoreboardUi), "Awake")]
    public class NoDupeScoreboardPatch
    {
        public static bool Lock = false;

        static bool Prefix()
        {
            if (Lock)
                return false;

            return true;
        }
    }

    // Adapted from Steel's built-in mutator published in the ravenscript channel on the Ravenfield Discord 
    public class GameUI : MonoBehaviour
    {
        public static GameUI instance;
        private GameObject nameTagPrefab;
        private bool onlyForTeam;
        private int playerTeamID;
        public float scaleMultiplier = 0.04f; // 0.06
        private int focusRange = 220;
        public Canvas ravenMUICanvas;
        private bool nameTagsEnabled;
        public int nameTagfontSize = 32;
        private bool customColor;
        private Color customColorEnemy;
        private Color customColorTeam;
        private Dictionary<NameTagData, Text> nameTagObjects = new Dictionary<NameTagData, Text>();
        private bool loaded = false;
        public class NameTagData
        {
            public Actor actor;
            public bool isDrawing;
            public bool usingDriverTag;
            public Color teamColor;
            public RectTransform parentTransform;
            public CanvasGroup canvasGroup;
        }
        void Awake()
        {
            Plugin.logger.LogInfo("Nametags Start");
            instance = this;
            onlyForTeam = LobbySystem.instance.nameTagsForTeamOnly;
            if (onlyForTeam)
                focusRange = focusRange * 2;
            SetCustomColor();
            OptionsPatch.onSettingUpdate += ToggleNameTags;
            StartCoroutine(LoadAssetBundle(Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.UIBundle")));
        }

        void OnDestroy()
        {
            OptionsPatch.onSettingUpdate -= ToggleNameTags;
        }

        void InitNameTags()
        {
            playerTeamID = ActorManager.instance.player.team;
            // Copy the Scoreboard Canvas instead of the IngameUI because it's easier to get rid of the other components
            NoDupeScoreboardPatch.Lock = true;
            GameObject scoreBoardCanvas = (typeof(IngameUI).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic)
                                                           .GetValue(IngameUI.instance) as Canvas).transform.parent.Find("Scoreboard Canvas").gameObject;
            ravenMUICanvas = Instantiate(scoreBoardCanvas, scoreBoardCanvas.transform.parent).GetComponent<Canvas>();
            ravenMUICanvas.enabled = true;
            DestroyImmediate(ravenMUICanvas.GetComponent<ScoreboardUi>());
            for (int x = 0; x < ravenMUICanvas.transform.childCount; x++)
            {
                DestroyImmediate(ravenMUICanvas.transform.GetChild(x).gameObject);
            }
            NoDupeScoreboardPatch.Lock = false;

            ToggleNameTags();
            loaded = true;
        }

        public void RemoveNameTag(Actor actor) 
        {
            var nametags = nameTagObjects.Where(kv => kv.Key.actor == actor).ToArray();

            foreach (var nametag in nametags) 
            {
                nametag.Value.enabled = false;
                nametag.Key.canvasGroup.alpha = 0f;

                nameTagObjects.Remove(nametag.Key);
            }
        }

        public void ToggleNameTags()
        {
            if (instance == null)
                return;
            bool parsedValue = bool.TryParse(SteamMatchmaking.GetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTags"),out bool nameTagsEnabled);
            if (parsedValue)
                LobbySystem.instance.nameTagsEnabled = nameTagsEnabled;
            bool parsedValue2 = bool.TryParse(SteamMatchmaking.GetLobbyData(LobbySystem.instance.ActualLobbyID, "nameTagsForTeamOnly"), out bool nameTagsTeamOnlyEnabled);
            if (parsedValue2)
                LobbySystem.instance.nameTagsForTeamOnly = nameTagsTeamOnlyEnabled;
            //LobbySystem.instance.nameTagsForTeamOnly;
            bool settingsNameTagEnabled = LobbySystem.instance.nameTagsEnabled;
            SetCustomColor();
            nameTagsEnabled = (OptionsPatch.showHUD && settingsNameTagEnabled);
            nameTagfontSize = Mathf.RoundToInt(OptionsPatch.GetOptionWithName<float>(OptionsPatch.RavenMOptions.NameTagScaleMultiplier, OptionsPatch.OptionTypes.Slider));
            foreach (var nameTag in nameTagObjects.Keys)
            {
                nameTag.parentTransform.gameObject.SetActive(nameTagsEnabled);
                Color color = GetColorForTeam(nameTag.actor.team);
                nameTag.teamColor = color;
                nameTagObjects[nameTag].color = color;
                if (LobbySystem.instance.nameTagsForTeamOnly)
                    if (nameTag.actor.team != playerTeamID)
                        nameTag.canvasGroup.alpha = 0f;
                nameTagObjects[nameTag].resizeTextMaxSize = nameTagfontSize;
                nameTagObjects[nameTag].resizeTextMinSize = nameTagfontSize - 10;
            }
            Plugin.logger.LogInfo("Toggled nametags");
        }
        public void AddToNameTagQueue(Actor actor)
        {
            StartCoroutine(WaitUntilReady(actor));
        }
        IEnumerator WaitUntilReady(Actor actor)
        {
            yield return new WaitUntil(() => loaded == true);
            UI.GameUI.instance.CreateNameTagInstance(actor, UI.GameUI.instance.ravenMUICanvas.GetComponent<RectTransform>());
        }
        public void CreateNameTagInstance(Actor actor,RectTransform canvasTransform)
        {
            bool settingsNameTagEnabled = LobbySystem.instance.nameTagsEnabled;
            nameTagsEnabled = (OptionsPatch.showHUD && settingsNameTagEnabled);
            if (!nameTagsEnabled)
            {
                return;
            }
            GameObject textInstance = Instantiate(nameTagPrefab, canvasTransform.transform);
            Image textParent = textInstance.GetComponent<Image>();
            Text nameTagText = textInstance.GetComponentInChildren<Text>();
            nameTagText.resizeTextForBestFit = true;
            nameTagText.resizeTextMaxSize = nameTagfontSize;
            nameTagText.resizeTextMinSize = nameTagfontSize - 10;
            textParent.rectTransform.SetParent(canvasTransform,false);
            NameTagData nameTagData = new NameTagData
            {
                actor = actor,
                isDrawing = false,
                usingDriverTag = false,
                teamColor = GetColorForTeam(actor.team),
                parentTransform = textParent.rectTransform,
                canvasGroup = textParent.GetComponent<CanvasGroup>()
            };
            nameTagObjects.Add(nameTagData, nameTagText);
            nameTagObjects[nameTagData].text = actor.name;
            nameTagObjects[nameTagData].color = nameTagData.teamColor;
            if (onlyForTeam)
                if (actor.team != playerTeamID)
                    nameTagData.canvasGroup.alpha = 0f;
            Plugin.logger.LogInfo($"Created nametag for {actor.name}");
        }
        private void SetCustomColor()
        {
            customColor = OptionsPatch.GetOptionWithName<bool>(OptionsPatch.RavenMOptions.CustomNameTagColor, OptionsPatch.OptionTypes.Toggle);
            if (customColor)
            {
                string customColorEnemyHex = OptionsPatch.GetOptionWithName<string>(OptionsPatch.OptionText.NameTagEnemyColor).Value;
                string customColorTeamHex = OptionsPatch.GetOptionWithName<string>(OptionsPatch.OptionText.NameTagTeamColor).Value;
                if (ColorUtility.TryParseHtmlString(customColorEnemyHex, out Color redColor))
                {
                    customColorEnemy = redColor;
                }
                if (ColorUtility.TryParseHtmlString(customColorTeamHex, out Color blueColor))
                {
                    customColorTeam = blueColor;
                }
            }
        }
        Color GetColorForTeam(int team)
        {
            Color color = ColorScheme.TeamColor(team);
            if (team == playerTeamID)
            {
                color = Color.green;
                if (customColor)
                    color = customColorTeam;
            }
            else
            {
                if (customColor)
                    color = customColorEnemy;
            }
            return color;
        }
        IEnumerator LoadAssetBundle(Stream path)
        {
            var bundleLoadRequest = AssetBundle.LoadFromStreamAsync(path);
            yield return bundleLoadRequest;

            var nameTagsAssetBundle = bundleLoadRequest.assetBundle;
            if (nameTagsAssetBundle == null)
            {
                Plugin.logger.LogError("Failed to load nameTagsAssetBundle");
                yield break;
            }

            var assetLoadRequest = nameTagsAssetBundle.LoadAssetAsync<GameObject>("PlayerNameTag");
            yield return assetLoadRequest;

            nameTagPrefab = assetLoadRequest.asset as GameObject;
            nameTagsAssetBundle.Unload(false);
            InitNameTags();
        }
        public void SetNameTagForActor(Actor actor, string newName)
        {
            foreach (var kv in nameTagObjects.Keys)
            {
                Actor actorValue = kv.actor;
                Text tag = nameTagObjects[kv];   
                if(actorValue == actor)
                {
                    tag.text = newName;
                }
            }
        }
        public void ResetNameTagsToOriginal()
        {
            foreach (var kv in nameTagObjects.Keys)
            {
                Actor actorValue = kv.actor;
                Text tag = nameTagObjects[kv];
                tag.text = actorValue.name;
            }
        }
        void LateUpdate()
        {
            if (!nameTagsEnabled)
            {
                return;
            }

            foreach (var kv in nameTagObjects.Keys)
            {
                Actor actor = kv.actor;

                if (((actor.controller as NetActorController).Flags & (int)ActorStateFlags.AiControlled) != 0)
                    continue;
                if (onlyForTeam)
                    if (actor.team != playerTeamID)
                        continue;
                var camera = FpsActorController.instance.inPhotoMode ? SpectatorCamera.instance.camera : FpsActorController.instance.GetActiveCamera();
                Vector3 currentPos = actor.CenterPosition() + new Vector3(0, 1f, 0);
                if (actor.IsSeated())
                {
                    if (!actor.dead && actor.seat.vehicle.rigidbody != null)
                    {
                        currentPos = actor.seat.vehicle.GetWorldCenterOfMass();
                    }
                    else
                    {
                        currentPos = actor.seat.vehicle.transform.position;
                    }
                }
                Vector3 wtsVector = camera.WorldToScreenPoint(currentPos);
               
                Text tag = nameTagObjects[kv];
                bool isTeammate = actor.team == playerTeamID;
                bool isDriver = actor.IsDriver();
                int focusSize = Screen.height / 3;
                bool isInView = wtsVector.z > 0f && ActorManager.ActorCanSeePlayer(actor);
                bool isInFocus = Mathf.Abs(wtsVector.x - (Screen.width / 2)) < focusSize && Mathf.Abs(wtsVector.y - Screen.height / 3) < focusSize;
                bool shouldDraw = isTeammate ? wtsVector.z > 0 : !onlyForTeam && isInView && !actor.dead && !actor.IsPassenger() && (isInFocus && wtsVector.z < focusRange);
                int scale = Mathf.RoundToInt((actor.CenterPosition() - camera.transform.position).magnitude * scaleMultiplier + nameTagfontSize);
                tag.fontSize = Mathf.Clamp(scale, nameTagfontSize - 10, nameTagfontSize);
                if (!kv.isDrawing && shouldDraw) {
                    kv.canvasGroup.alpha = 1f;
                    kv.isDrawing = true;
                } else if(kv.isDrawing && !shouldDraw) {
                    kv.canvasGroup.alpha = 0f;
                    kv.isDrawing = false;
                }
                if (shouldDraw)
                {
                    if (isDriver)
                    {
                        int vehiclePassengers = -1;
                        foreach(Seat seat in actor.seat.vehicle.seats)
                        {
                            if (seat.IsOccupied())
                            {
                                vehiclePassengers += 1;
                            }
                        }
                        if (vehiclePassengers > 0)
                        {
                            tag.text = $"{actor.name} + {vehiclePassengers}";
                        }
                        kv.usingDriverTag = true;

                    }
                    else if (!isDriver && kv.usingDriverTag)
                    {
                        tag.text = actor.name;
                        kv.usingDriverTag = false;
                    }
                }
                kv.parentTransform.position = wtsVector;
                if (shouldDraw)
                {
                    tag.enabled = true;
                    kv.canvasGroup.alpha = 1f;
                }
                else
                {
                    tag.enabled = false;
                    kv.canvasGroup.alpha = 0f;
                }
            }

        }
    }
}
