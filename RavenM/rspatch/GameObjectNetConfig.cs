using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenM.rspatch
{
    public struct GameObjectNetConfig
    {
        public bool HostOnly;
        public bool OnlySyncIfChanged;
        public bool SyncPosition;
        public bool SyncRotation;
        public bool SyncScale;
        public float TickRate;

        public GameObjectNetConfig(bool hostOnly, bool onlySyncIfChanged, bool syncPosition, bool syncRotation,bool syncScale, float tickRate)
        {
            HostOnly = hostOnly;
            OnlySyncIfChanged = onlySyncIfChanged;
            SyncPosition = syncPosition;
            SyncRotation = syncRotation;
            SyncScale = syncScale;
            TickRate = tickRate;
        }
        public GameObjectNetConfig(GameObjectNetConfig config)
        {
            HostOnly = config.HostOnly;
            OnlySyncIfChanged = config.OnlySyncIfChanged;
            SyncPosition = config.SyncPosition;
            SyncRotation = config.SyncRotation;
            SyncScale = config.SyncScale;
            TickRate = config.TickRate;
        }
    }
}
