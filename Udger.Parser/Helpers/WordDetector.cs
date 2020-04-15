/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System;
using System.Collections.Generic;

namespace Udger.Parser.Helpers
{
    internal class WordDetector
    {
        private struct WordInfo
        {
            public int Id { get; }
            public string Word { get; }

            public WordInfo(int id, string word)
            {
                Id = id;
                Word = word;
            }
        }

        private static readonly int ARRAY_DIMENSION = 'z' - 'a';
        private static readonly int ARRAY_SIZE = (ARRAY_DIMENSION + 1) * (ARRAY_DIMENSION + 1);

        private readonly List<WordInfo>[] wordArray;
        private int minWordSize = int.MaxValue;
        private bool isFrozen;

        public WordDetector()
        {
            wordArray = new List<WordInfo>[ARRAY_SIZE];
        }

        public void AddWord(int id, string word)
        {
            if (isFrozen)
                throw new Exception("Cannot add words after the WordDetector is frozen");

            if (word.Length < minWordSize)
                minWordSize = word.Length;

            var s = word.ToLower();
            var index = (s[0] - 'a') * ARRAY_DIMENSION + s[1] - 'a';
            if (index < 0 || index >= ARRAY_SIZE)
                return;

            var wList = wordArray[index];
            if (wList == null)
            {
                wList = new List<WordInfo>();
                wordArray[index] = wList;
            }
            wList.Add(new WordInfo(id, s));
        }

        public HashSet<int> FindWords(string text)
        {
            if (!isFrozen)
                throw new Exception("Cannot find words before the WordDetector is frozen");

            var ret = new HashSet<int>();

            var s = text.ToLower();
            var dimension = 'z' - 'a';
            for (var i = 0; i < s.Length - (minWordSize - 1); i++)
            {
                var c1 = s[i];
                var c2 = s[i + 1];
                if (c1 < 'a' || c1 > 'z' || c2 < 'a' || c2 > 'z')
                    continue;

                var index = (c1 - 'a') * dimension + c2 - 'a';
                var l = wordArray[index];
                if (l == null)
                    continue;

                foreach (var wi in l)
                {
                    if (s.Substring(i).StartsWith(wi.Word))
                        ret.Add(wi.Id);
                }
            }

            return ret;
        }

        public void Freeze()
        {
            isFrozen = true;
        }
    }
}
