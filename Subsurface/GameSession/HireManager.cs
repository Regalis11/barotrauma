﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Subsurface
{
    class HireManager
    {
        public List<CharacterInfo> availableCharacters;

        const int MaxAvailableCharacters = 10;

        public HireManager()
        {
            availableCharacters = new List<CharacterInfo>();
        }

        public void GenerateCharacters(string file, int amount)
        {
            for (int i = 0 ; i<amount ; i++)
            {
                availableCharacters.Add(new CharacterInfo(file));
                Debug.Write(availableCharacters.Last().name);
            }
        }
    }
}
