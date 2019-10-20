﻿using Kermalis.PokemonGameEngine.Util;

namespace Kermalis.PokemonGameEngine.Overworld
{
    internal sealed class Tileset
    {
        private readonly uint[][][] _tiles;

        public Tileset(string resource)
        {
            _tiles = RenderUtil.LoadSpriteSheet(resource, 8, 8);
        }

        public unsafe void DrawBlock(uint* bmpAddress, int bmpWidth, int bmpHeight, Blockset.Block block, int x, int y)
        {
            for (int z = 0; z < byte.MaxValue + 1; z++)
            {
                void Draw(Blockset.Block.Tile[] layers, int tx, int ty)
                {
                    for (int t = 0; t < layers.Length; t++)
                    {
                        Blockset.Block.Tile tile = layers[t];
                        if (tile.ZLayer == z)
                        {
                            RenderUtil.Draw(bmpAddress, bmpWidth, bmpHeight, tx, ty, _tiles[tile.TilesetTileNum], tile.XFlip, tile.YFlip);
                        }
                    }
                }
                Draw(block.TopLeft, x, y);
                Draw(block.TopRight, x + 8, y);
                Draw(block.BottomLeft, x, y + 8);
                Draw(block.BottomRight, x + 8, y + 8);
            }
        }
    }
}
