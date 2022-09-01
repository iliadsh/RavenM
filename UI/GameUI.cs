using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RavenM.UI
{
    [HarmonyPatch(typeof(IngameUi),"Awake")]
    public class IngameUIPatch
    {
       static void Postfix()
       {
            IngameUi.instance.gameObject.AddComponent<GameUI>();
       }
    }
    // Adapted from Steel's built-in mutator published in the ravenscript channel on the Ravenfield Discord 
    public class GameUI : MonoBehaviour
    {
        public static GameUI instance;
        private GameObject nameTagPrefab;
        private bool onlyForTeam;
        private int teamID;
        public float scaleMultiplier = 0.04f; // 0.06
        private int focusRange = 220;
        private Canvas ravenMUICanvas;
        private bool nameTagsEnabled;
        public int nameTagfontSize = 32;
        private bool customColor;
        private Color customColorEnemy;
        private Color customColorTeam;
        private Dictionary<NameTagData, Text> nameTagObjects = new Dictionary<NameTagData, Text>();
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
            instance = this;
            if (onlyForTeam)
                focusRange = 1200;
            onlyForTeam = LobbySystem.instance.nameTagsForTeamOnly;
            SetCustomColor();
            OptionsPatch.onSettingUpdate += ToggleNameTags;
            StartCoroutine(LoadAssetBundle(Assembly.GetExecutingAssembly().GetManifestResourceStream("RavenM.assets.UIBundle")));
        }
        void InitNameTags()
        {
            teamID = FpsActorController.instance.actor.team;
            // Copy the Scoreboard Canvas instead of the IngameUI because it's easier to get rid of the other components
            GameObject scoreBoardCanvas = IngameUi.instance.canvas.transform.parent.Find("Scoreboard Canvas").gameObject;
            ravenMUICanvas = GameObject.Instantiate(scoreBoardCanvas, scoreBoardCanvas.transform.parent).GetComponent<Canvas>();
            ravenMUICanvas.enabled = true;
            MonoBehaviour.Destroy(ravenMUICanvas.GetComponent<ScoreboardUi>());
            for (int x = 0; x < ravenMUICanvas.transform.childCount; x++)
            {
                GameObject.Destroy(ravenMUICanvas.transform.GetChild(x).gameObject);
            }

            RectTransform canvasRectTransform = ravenMUICanvas.GetComponent<RectTransform>();


            foreach (var kv in IngameNetManager.instance.ClientActors)
            //foreach (var kv in ActorManager.instance.actors)
            {
                var id = kv.Key;
                var actor = kv.Value;

                if (IngameNetManager.instance.OwnedActors.Contains(id))
                    continue;

                var controller = actor.controller as NetActorController;

                if ((controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
                    continue;
                CreateNameTagInstance(actor, canvasRectTransform);
            }
            ToggleNameTags();
        }
        public void ToggleNameTags()
        {
            if (instance == null)
                return;
            bool settingsNameTagEnabled = LobbySystem.instance.nameTagsEnabled;
            SetCustomColor();
            nameTagsEnabled = (OptionsPatch.showHUD && settingsNameTagEnabled);
            foreach (var nameTag in nameTagObjects.Keys)
            {
                nameTag.parentTransform.gameObject.SetActive(nameTagsEnabled);
                Color color = GetColorForTeam(nameTag.actor.team);
                nameTag.teamColor = color;
                nameTagObjects[nameTag].color = color;
            }
            nameTagfontSize = Mathf.RoundToInt(OptionsPatch.GetOptionWithName<float>(OptionsPatch.RavenMOptions.NameTagScaleMultiplier, OptionsPatch.OptionTypes.Slider));
        }
        void CreateNameTagInstance(Actor actor,RectTransform canvasTransform)
        {
            if (onlyForTeam) {
                if (actor.team == teamID)
                    return;
            }
            GameObject textInstance = GameObject.Instantiate(nameTagPrefab, canvasTransform.transform);
            Image textParent = textInstance.GetComponent<Image>();
            Text nameTagText = textInstance.GetComponentInChildren<Text>();
            nameTagText.resizeTextForBestFit = true;
            nameTagText.resizeTextMaxSize = nameTagfontSize;
            nameTagText.resizeTextMinSize = nameTagfontSize - 10;
            textParent.rectTransform.SetParent(canvasTransform,false);
            NameTagData nameTagData = new NameTagData();
            nameTagData.actor = actor;
            nameTagData.isDrawing = false;
            nameTagData.usingDriverTag = false;
            nameTagData.teamColor = GetColorForTeam(actor.team);
            nameTagData.parentTransform = textParent.rectTransform;
            nameTagData.canvasGroup = textParent.GetComponent<CanvasGroup>(); 
            nameTagObjects.Add(nameTagData, nameTagText);
            nameTagObjects[nameTagData].text = actor.name;
            nameTagObjects[nameTagData].color = nameTagData.teamColor;
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
            if (team == teamID)
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
            if (onlyForTeam)
            {
                color = Color.white;
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
        void Update()
        {
            if (!nameTagsEnabled)
            {
                return;
            }
            //foreach (var kv in IngameNetManager.instance.ClientActors)
            //{
            //    var id = kv.Key;
            //    var actor = kv.Value;

            //    if (IngameNetManager.instance.OwnedActors.Contains(id))
            //        continue;

            //    var controller = actor.controller as NetActorController;

            //    if ((controller.Flags & (int)ActorStateFlags.AiControlled) != 0)
            //        continue;
            foreach (var kv in nameTagObjects.Keys)
            {
                Actor actor = kv.actor;
                // Implement mid game leaving
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
                bool isTeammate = actor.team == teamID;
                if (!onlyForTeam)
                    isTeammate = true;
                bool isDriver = actor.IsDriver();
                int focusSize = Screen.height / 3;
                float lookingAtSize = Screen.height / 1.5f;
                bool isInView = wtsVector.z > 0f && ActorManager.ActorCanSeePlayer(actor);
                bool isLookingAt = Mathf.Abs(wtsVector.x - (Screen.width / 3)) < lookingAtSize && Mathf.Abs(wtsVector.y - Screen.height / 4) < lookingAtSize;
                bool isInFocus = Mathf.Abs(wtsVector.x - (Screen.width / 2)) < focusSize && Mathf.Abs(wtsVector.y - Screen.height / 3) < focusSize;
                bool shouldDraw = isInView && !actor.dead && !actor.IsPassenger() && (isInFocus && wtsVector.z < focusRange) && isTeammate;
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
                tag.text = "" + Mathf.Abs(wtsVector.x - (Screen.width / 4)) + " | " + lookingAtSize;
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
