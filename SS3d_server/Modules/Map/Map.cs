﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Media;
using SS3d_server.Tiles;
using Lidgren.Network;

using SS3D_shared;
using SS3D_shared.HelperClasses;

namespace SS3d_server.Modules.Map
{
    public class Map
    {
        #region Variables
        private Tile[,] tileArray;
        SS3DNetserver netServer;
        private int mapWidth;
        private int mapHeight;
        private string[,] nameArray;
        public int tileSpacing = 64;
        private int wallHeight = 40; // This must be the same as defined in the MeshManager.
        DateTime lastAtmosDisplayPush;
        #endregion

        public Map(SS3DNetserver _netServer)
        {
            netServer = _netServer;
        }

        #region Startup
        public bool InitMap(string mapName)
        {
            if (!LoadMap(mapName))
            {
                NewMap();
            }
            else
            {
                ParseNameArray();
            }
            InitializeAtmos();

            return true;
        }
        #endregion

        #region Networking
        public void HandleNetworkMessage(NetIncomingMessage message)
        {
            MapMessage messageType = (MapMessage)message.ReadByte();
            switch (messageType)
            {
                case MapMessage.TurfClick:
                    HandleTurfClick(message);
                    break;
                case MapMessage.TurfUpdate:
                    HandleTurfUpdate(message);
                    break;
                default:
                    break;
            }

        }

        private void HandleTurfClick(NetIncomingMessage message)
        {
            // Who clicked and on what tile.
            Atom.Atom clicker = netServer.playerManager.GetSessionByConnection(message.SenderConnection).attachedAtom;
            short x = message.ReadInt16();
            short y = message.ReadInt16();

            if (Vector2.Distance(clicker.position, new Vector2(x * tileSpacing + (tileSpacing / 2), y * tileSpacing + (tileSpacing / 2))) > 96)
            {
                return; // They were too far away to click us!
            }
            bool Update = false;
            if (IsSaneArrayPosition(x, y))
            {
                Update = tileArray[x, y].ClickedBy(clicker);
                if (Update)
                {
                    if (tileArray[x, y].tileState == TileState.Dead)
                    {
                        Tiles.Atmos.GasCell g = tileArray[x, y].gasCell;
                        Tiles.Tile t = GenerateNewTile(x, y, tileArray[x, y].tileType);
                        tileArray[x, y] = t;
                        tileArray[x, y].gasCell = g;
                    }
                    NetworkUpdateTile(x, y);
                }
            }
        }

        private void DestroyWall(Point arrayPosition)
        {
            if (IsSaneArrayPosition(arrayPosition.x, arrayPosition.y))
            {
                var t = tileArray[arrayPosition.x, arrayPosition.y];
                var g = t.gasCell;
                Tiles.Tile newTile = GenerateNewTile(arrayPosition.x, arrayPosition.y, TileType.Floor);
                tileArray[arrayPosition.x, arrayPosition.y] = newTile;
                newTile.gasCell = g;
                g.AttachToTile(newTile);
                NetworkUpdateTile(arrayPosition.x, arrayPosition.y);
            }                
        }

        private void HandleTurfUpdate(NetIncomingMessage message)
        {
            short x = message.ReadInt16();
            short y = message.ReadInt16();
            TileType type = (TileType)message.ReadByte();

            if (IsSaneArrayPosition(x, y))
            {
                Tiles.Atmos.GasCell g = tileArray[x, y].gasCell;
                Tile t = GenerateNewTile(x, y, type);
                tileArray[x, y] = t;
                tileArray[x, y].gasCell = g;
                g.AttachToTile(t);
                NetworkUpdateTile(x, y);
            }
        }

        public void NetworkUpdateTile(int x, int y)
        {
            if (!IsSaneArrayPosition(x, y))
                return;

            NetOutgoingMessage message = netServer.netServer.CreateMessage();
            message.Write((byte)NetMessage.MapMessage);
            message.Write((byte)MapMessage.TurfUpdate);
            message.Write((short)x);
            message.Write((short)y);
            message.Write((byte)tileArray[x, y].tileType);
            message.Write((byte)tileArray[x, y].tileState);
            netServer.SendMessageToAll(message);
        }
        #endregion

        #region Map loading/sending
        private bool LoadMap(string filename)
        {
            if (!File.Exists(filename))
            {
                return false;
            }

            FileStream fs = new FileStream(filename, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            mapWidth = int.Parse(sr.ReadLine());
            mapHeight = int.Parse(sr.ReadLine());

            nameArray = new string[mapWidth, mapHeight];

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    nameArray[x, y] = sr.ReadLine();
                }
            }

            sr.Close();
            fs.Close();

            return true;
        }

        public void SaveMap()
        {
            string fileName = "SavedMap";

            FileStream fs = new FileStream(fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            Console.WriteLine("Saving map: W: " + mapWidth + " H: " + mapHeight);
            sw.WriteLine(mapWidth);
            sw.WriteLine(mapHeight);
            
            for (int y = 0; y < mapHeight; y++)
            {
                Console.Write(".");
                for (int x = 0; x < mapWidth; x++)
                {
                    sw.WriteLine(tileArray[x, y].tileType.ToString());
                }
            }
            Console.Write("Done!");

            sw.Close();
            fs.Close();
        }

        private void NewMap()
        {
            Console.WriteLine("***** Cannot find map. Generating blank map. *****");
            mapWidth = 50;
            mapHeight = 50;
            tileArray = new Tile[mapWidth, mapHeight];
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    tileArray[x,y] = new Tiles.Floor.Floor(x, y, this);
                }
            }
        }

        private void ParseNameArray()
        {
            tileArray = new Tile[mapWidth, mapHeight];

            for (int z = 0; z < mapHeight; z++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    switch (nameArray[x, z])
                    {
                        case "wall":
                        case  "Wall":
                            tileArray[x, z] = new Tiles.Wall.Wall(x, z, this);
                            break;
                        case "floor":
                        case "Floor":
                            tileArray[x, z] = new Tiles.Floor.Floor(x, z, this);
                            break;
                        case "space":
                        case "Space":
                            tileArray[x, z] = new Tiles.Floor.Space(x, z, this);
                            break;
                        default:
                            break;
                    }
                }
            }
        }




        public TileType[,] GetMapForSending()
        {
            TileType[,] mapObjectType = new TileType[mapWidth, mapHeight];

            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapHeight; z++)
                {
                    mapObjectType[x, z] = tileArray[x, z].tileType;
                }
            }

            return mapObjectType;

        }

        public Tile GetTileAt(int x, int y)
        {
            if (!IsSaneArrayPosition(x, y))
                return null;
            return tileArray[x, y];
        }

        #endregion

        #region Atmos

        private void InitializeAtmos()
        {
            for (int z = 0; z < mapHeight; z++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    tileArray[x, z].gasCell = new Tiles.Atmos.GasCell(tileArray[x, z], x, z, tileArray, mapWidth, mapHeight);
                    if (tileArray[x, z].tileType == TileType.Floor)
                    {
                        tileArray[x, z].gasCell.AddGas(20, GasType.Oxygen);
                        tileArray[x, z].gasCell.AddGas(80, GasType.Nitrogen);
                    }
                }
            }
        }

        private struct AtmosRecord
        {
            int x;
            int y;
            byte display;

            public AtmosRecord(int _x, int _y, byte _display)
            {
                x = _x;
                y = _y;
                display = _display;
            }

            public void pack(NetOutgoingMessage message)
            {
                message.Write(x);
                message.Write(y);
                message.Write(display);
            }
        }

        public void AddGasAt(Point position, GasType type, int amount)
        {
            tileArray[position.x, position.y].gasCell.AddGas(amount, type);
        }

        public void UpdateAtmos()
        {
            for (int x = 0; x < mapWidth; x++)
                for (int y = 0; y < mapHeight; y++)
                {
                    tileArray[x, y].gasCell.CalculateNextGasAmount();
                }
            for (int x = 0; x < mapWidth; x++)
                for (int y = 0; y < mapHeight; y++)
                {
                    if (tileArray[x, y].tileState == TileState.Dead && tileArray[x, y].tileType == TileType.Wall)
                        DestroyWall(new Point(x, y));
                    tileArray[x, y].gasCell.Update();
                }

            //Prepare gas display message
            if ((DateTime.Now - lastAtmosDisplayPush).TotalMilliseconds > 333)
            {
                bool sendUpdate = false;
                NetOutgoingMessage message = netServer.netServer.CreateMessage();
                message.Write((byte)NetMessage.AtmosDisplayUpdate);

                List<AtmosRecord> records = new List<AtmosRecord>();
                for (int x = 0; x < mapWidth; x++)
                    for (int y = 0; y < mapHeight; y++)
                    {
                        byte[] displayBytes = tileArray[x, y].gasCell.PackDisplayBytes();
                        if (displayBytes.Length == 0) //if there are no changes, continue.
                            continue;
                        sendUpdate = true;
                        foreach (byte displayByte in displayBytes)
                        {
                            records.Add(new AtmosRecord(x, y, displayByte));
                        }
                    }

                if (sendUpdate)
                {
                    message.Write(records.Count);
                    foreach (AtmosRecord rec in records)
                    {
                        rec.pack(message);
                    }
                    netServer.SendMessageToAll(message, NetDeliveryMethod.Unreliable);// Gas updates aren't a big deal.
                    Console.Write("Sending Gas update with " + records.Count.ToString() + " records\n");
                }
                lastAtmosDisplayPush = DateTime.Now;
            }
        }

        public void SendAtmosStateTo(NetConnection client)
        {
            NetOutgoingMessage message = netServer.netServer.CreateMessage();
            message.Write((byte)NetMessage.AtmosDisplayUpdate);

            List<AtmosRecord> records = new List<AtmosRecord>();
            for (int x = 0; x < mapWidth; x++)
                for (int y = 0; y < mapHeight; y++)
                {
                    byte[] displayBytes = tileArray[x, y].gasCell.PackDisplayBytes(true);
                    if (displayBytes.Length == 0) //if there are no changes, continue.
                        continue;
                    foreach (byte displayByte in displayBytes)
                    {
                        records.Add(new AtmosRecord(x, y, displayByte));
                    }
                }

            message.Write(records.Count);
            foreach (AtmosRecord rec in records)
            {
                rec.pack(message);
            }
            netServer.SendMessageTo(message, client, NetDeliveryMethod.Unreliable);// Gas updates aren't a big deal.
            Console.Write("Sending Gas update to " + netServer.playerManager.GetSessionByConnection(client).name + "\n");
        }
        #endregion

        #region Map altering
        public bool ChangeTile(int x, int z, TileType newType)
        {
            if (x < 0 || z < 0)
                return false;
            if (x > mapWidth || z > mapWidth)
                return false;

            Tile tile = GenerateNewTile(x, z, newType);

            tileArray[x, z] = tile;
            return true;
        }

        public Tile GenerateNewTile(int x, int y, TileType type)
        {
            switch (type)
            {
                case TileType.Space:
                    Tiles.Floor.Space space = new Tiles.Floor.Space(x, y, this);
                    return space;
                case TileType.Floor:
                    Tiles.Floor.Floor floor = new Tiles.Floor.Floor(x, y, this);
                    return floor;
                case TileType.Wall:
                    Tiles.Wall.Wall wall = new Tiles.Wall.Wall(x, y, this);
                    return wall;
                default:
                    return null;
            }
        }
        #endregion

        #region networking
        public NetOutgoingMessage CreateMapMessage(MapMessage messageType)
        {
            NetOutgoingMessage message = netServer.netServer.CreateMessage();
            message.Write((byte)NetMessage.MapMessage);
            message.Write((byte)messageType);
            return message;
        }

        /// <summary>
        /// Lol fuck
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(NetOutgoingMessage message)
        {
            netServer.SendMessageToAll(message);
        }
        #endregion


        public int GetMapWidth()
        {
            return mapWidth;
        }

        public int GetMapHeight()
        {
            return mapHeight;
        }


        #region Tile helper function
        public Point GetTileArrayPositionFromWorldPosition(double x, double z)
        {
            
            if (x < 0 || z < 0)
                return new Point(-1, -1);
            if (x >= mapWidth * tileSpacing || z >= mapWidth * tileSpacing)
                return new Point(-1, -1);

            // We use floor here, because even if we're at pos 10.999999, we're still on tile 10 in the array.
            int xPos = (int)System.Math.Floor(x / tileSpacing);
            int zPos = (int)System.Math.Floor(z / tileSpacing);

            return new Point(xPos, zPos);
        }

        public Point GetTileArrayPositionFromWorldPosition(Vector2 pos)
        {
            return GetTileArrayPositionFromWorldPosition(pos.X, pos.Y);
        }

        public TileType GetObjectTypeFromWorldPosition(float x, float z)
        {
            Point arrayPosition = GetTileArrayPositionFromWorldPosition(x, z);
            if (arrayPosition.x < 0 || arrayPosition.y < 0)
            {
                return TileType.None;
            }
            else
            {
                return GetObjectTypeFromArrayPosition((int)arrayPosition.x, (int)arrayPosition.y);
            }
        }

        private TileType GetObjectTypeFromWorldPosition(Vector2 pos)
        {
            Point arrayPosition = GetTileArrayPositionFromWorldPosition(pos.X, pos.Y);
            if (arrayPosition.x < 0 || arrayPosition.y < 0)
            {
                return TileType.None;
            }
            else
            {
                return GetObjectTypeFromArrayPosition((int)arrayPosition.x, (int)arrayPosition.y);
            }
        }

        private TileType GetObjectTypeFromArrayPosition(int x, int z)
        {
            if (x < 0 || z < 0 || x >= mapWidth || z >= mapHeight)
            {
                return TileType.None;
            }
            else
            {
                return tileArray[x, z].tileType;
            }
        }

        public bool IsSaneArrayPosition(int x, int y)
        {
            if (x < 0 || y < 0)
                return false;
            if (x > mapWidth|| y > mapWidth)
                return false;
            return true;
        }
        #endregion


        #region BASIC collision things

        public bool CheckCollision(Vector2 pos)
        {
            TileType tile = GetObjectTypeFromWorldPosition(pos);

            if (tile == TileType.None)
            {
                return false;
            }
            else if (tile == TileType.Wall && pos.Y <= wallHeight)
            {
                return true;
            }
            else if ((tile == TileType.Floor || tile == TileType.Space) && pos.Y <= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public TileType GetObjectTypeAt(Vector2 pos)
        {
            return GetObjectTypeFromWorldPosition(pos);
        }

        public bool IsFloorUnder(Vector2 pos)
        {
            if (GetObjectTypeFromWorldPosition(pos) == TileType.Floor)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
    }
}
