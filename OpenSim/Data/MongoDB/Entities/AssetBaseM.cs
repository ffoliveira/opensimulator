using System;
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.MongoDB
{
    public class AssetBaseM : AssetBase
    {
        public AssetBaseM() : base() { }
        public AssetBaseM(string assetID, string name, sbyte assetType, string creatorID) : base(assetID, name, assetType, creatorID) { }
        public AssetBaseM(UUID assetID, string name, sbyte assetType, string creatorID) : base(assetID, name, assetType, creatorID) { }

        public int create_time { get; set; }
        public int access_time { get; set; }
    }
}
