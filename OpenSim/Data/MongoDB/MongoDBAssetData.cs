/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using MongoDB;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace OpenSim.Data.MongoDB
{
    /// <summary>
    /// A MongoDB Interface for the Asset Server
    /// </summary>
    public class MongoDBAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;
        private string m_mongodbname;
        private long m_ticksToEpoch;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        #region IPlugin Members

        public override string Version { get { return "1.0.0.0"; } }

        /// <summary>
        /// <para>Initialises Asset interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MongoDB storage plugin.</item>
        /// <item>Warns and uses the obsolete MongoDB_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        public override void Initialise(string connect)
        {
            m_ticksToEpoch = new System.DateTime(1970, 1, 1).Ticks;

            m_connectionString = connect;

            m_mongodbname = MongoUrl.Create(m_connectionString).DatabaseName;

            /*
             * TODO: IMPLEMENT MONGODB MIGRATION
            using (MongoDBConnection dbcon = new MongoDBConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "AssetStore");
                m.Update();
            }*/
        }

        public override void Initialise()
        {
            throw new NotImplementedException();
        }

        public override void Dispose() { }

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        override public string Name
        {
            get { return "MongoDB Asset storage engine"; }
        }

        #endregion

        #region IAssetDataPlugin Members

        /// <summary>
        /// Fetch Asset <paramref name="assetID"/> from database
        /// </summary>
        /// <param name="assetID">Asset UUID to fetch</param>
        /// <returns>Return the asset</returns>
        /// <remarks>On failure : throw an exception and attempt to reconnect to database</remarks>
        override public AssetBase GetAsset(UUID assetID)
        {
            AssetBaseM asset = null;

            try
            {
                MongoDatabase dbdata = getMongoDatabase();

                MongoCollection<AssetBaseM> dbcolecao = dbdata.GetCollection<AssetBaseM>("assets");

                IMongoQuery query = Query<AssetBaseM>.EQ(e => e.ID, assetID.ToString());

                asset = dbcolecao.FindOne(query);

            }
            catch (Exception e)
            {
                m_log.Error( string.Format("[ASSETS DB]: MongoDB failure fetching asset {0}.  Exception  ", assetID), e);
            }

            return (AssetBase)asset;
        }

        public MongoDatabase getMongoDatabase()
        {
            return new MongoClient(m_connectionString).GetServer().GetDatabase(m_mongodbname);
        }

        /// <summary>
        /// Create an asset in database, or update it if existing.
        /// </summary>
        /// <param name="asset">Asset UUID to create</param>
        /// <remarks>On failure : Throw an exception and attempt to reconnect to database</remarks>
        override public void StoreAsset(AssetBase asset)
        {
            try
            {
                string assetName = asset.Name;
                if (asset.Name.Length > AssetBase.MAX_ASSET_NAME)
                {
                    assetName = asset.Name.Substring(0, AssetBase.MAX_ASSET_NAME);
                    m_log.WarnFormat(
                        "[ASSET DB]: Name '{0}' for asset {1} truncated from {2} to {3} characters on add",
                        asset.Name, asset.ID, asset.Name.Length, assetName.Length);
                }

                string assetDescription = asset.Description;
                if (asset.Description.Length > AssetBase.MAX_ASSET_DESC)
                {
                    assetDescription = asset.Description.Substring(0, AssetBase.MAX_ASSET_DESC);
                    m_log.WarnFormat(
                        "[ASSET DB]: Description '{0}' for asset {1} truncated from {2} to {3} characters on add",
                        asset.Description, asset.ID, asset.Description.Length, assetDescription.Length);
                }

                MongoDatabase dbdata = getMongoDatabase();

                MongoCollection<AssetBaseM> dbcolecao = dbdata.GetCollection<AssetBaseM>("assets");

                IMongoQuery query = Query<AssetBaseM>.EQ(e => e.ID, asset.ID);
                AssetBaseM assetRet = dbcolecao.FindOne(query);

                int now = (int)((System.DateTime.Now.Ticks - m_ticksToEpoch) / 10000000);

                if (assetRet == null)
                {
                    assetRet = (AssetBaseM)asset;

                    assetRet.create_time = now;
                    assetRet.access_time = now;
                    dbcolecao.Insert(assetRet);
                }
                else
                {
                    assetRet = (AssetBaseM)asset;
                    assetRet.access_time = now;

                    dbcolecao.Save(assetRet);
                }

            }
            catch (Exception e)
            {
                m_log.Error(
                    string.Format(
                        "[ASSET DB]: MongoDB failure creating asset {0} with name {1}.  Exception  ",
                        asset.FullID, asset.Name)
                    , e);
            }

        }

        private void UpdateAccessTime(AssetBase asset)
        {

            using (MongoDBConnection dbcon = new MongoDBConnection(m_connectionString))
            {
                dbcon.Open();

                using (MongoDBCommand cmd
                    = new MongoDBCommand("update assets set access_time=?access_time where id=?id", dbcon))
                {
                    try
                    {
                        using (cmd)
                        {
                            // create unix epoch time
                            int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);
                            cmd.Parameters.AddWithValue("?id", asset.ID);
                            cmd.Parameters.AddWithValue("?access_time", now);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[ASSETS DB]: Failure updating access_time for asset {0} with name {1}.  Exception  ", 
                                asset.FullID, asset.Name), 
                            e);
                    }
                }
            }
        }

        /// <summary>
        /// Check if the assets exist in the database.
        /// </summary>
        /// <param name="uuidss">The assets' IDs</param>
        /// <returns>For each asset: true if it exists, false otherwise</returns>
        public override bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return new bool[0];

            HashSet<UUID> exist = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string sql = string.Format("SELECT id FROM assets WHERE id IN ({0})", ids);

            using (MongoDBConnection dbcon = new MongoDBConnection(m_connectionString))
            {
                dbcon.Open();
                using (MongoDBCommand cmd = new MongoDBCommand(sql, dbcon))
                {
                    using (MongoDBDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = DBGuid.FromDB(dbReader["id"]);
                            exist.Add(id);
                        }
                    }
                }
            }

            bool[] results = new bool[uuids.Length];
            for (int i = 0; i < uuids.Length; i++)
                results[i] = exist.Contains(uuids[i]);

            return results;
        }

        /// <summary>
        /// Returns a list of AssetMetadata objects. The list is a subset of
        /// the entire data set offset by <paramref name="start" /> containing
        /// <paramref name="count" /> elements.
        /// </summary>
        /// <param name="start">The number of results to discard from the total data set.</param>
        /// <param name="count">The number of rows the returned list should contain.</param>
        /// <returns>A list of AssetMetadata objects.</returns>
        public override List<AssetMetadata> FetchAssetMetadataSet(int start, int count)
        {
            List<AssetMetadata> retList = new List<AssetMetadata>(count);

            using (MongoDBConnection dbcon = new MongoDBConnection(m_connectionString))
            {
                dbcon.Open();

                using (MongoDBCommand cmd
                    = new MongoDBCommand(
                        "SELECT name,description,assetType,temporary,id,asset_flags,CreatorID FROM assets LIMIT ?start, ?count",
                        dbcon))
                {
                    cmd.Parameters.AddWithValue("?start", start);
                    cmd.Parameters.AddWithValue("?count", count);

                    try
                    {
                        using (MongoDBDataReader dbReader = cmd.ExecuteReader())
                        {
                            while (dbReader.Read())
                            {
                                AssetMetadata metadata = new AssetMetadata();
                                metadata.Name = (string)dbReader["name"];
                                metadata.Description = (string)dbReader["description"];
                                metadata.Type = (sbyte)dbReader["assetType"];
                                metadata.Temporary = Convert.ToBoolean(dbReader["temporary"]); // Not sure if this is correct.
                                metadata.Flags = (AssetFlags)Convert.ToInt32(dbReader["asset_flags"]);
                                metadata.FullID = DBGuid.FromDB(dbReader["id"]);
                                metadata.CreatorID = dbReader["CreatorID"].ToString();

                                // Current SHA1s are not stored/computed.
                                metadata.SHA1 = new byte[] { };

                                retList.Add(metadata);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format(
                                "[ASSETS DB]: MongoDB failure fetching asset set from {0}, count {1}.  Exception  ", 
                                start, count), 
                            e);
                    }
                }
            }

            return retList;
        }

        public override bool Delete(string id)
        {
            using (MongoDBConnection dbcon = new MongoDBConnection(m_connectionString))
            {
                dbcon.Open();

                using (MongoDBCommand cmd = new MongoDBCommand("delete from assets where id=?id", dbcon))
                {
                    cmd.Parameters.AddWithValue("?id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            return true;
        }

        #endregion
    }

}