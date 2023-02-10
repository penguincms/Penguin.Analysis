using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Enumeration : BaseColumn
    {
        #region Fields

        protected Dictionary<string, EnumOption> valuesDict = new();

        public override int OptionCount => Values.Length;

        public override bool SeedMe => true;

        public string[] Values { get; set; }

        public Enumeration() : base()
        {
        }

        #endregion Fields

        #region Classes

        [Serializable]
        public static class GlobalSettings
        {
            #region Properties

            /// <summary>
            /// The minimum number of times an instance has to be seen to be analyzed
            /// </summary>
            public static int MinimumInstances { get; set; } = 5;

            #endregion Properties
        }

        [Serializable]
        public class EnumOption
        {
            #region Properties

            public string Display { get; set; }

            public int Index { get; set; }

            public int Indicators { get; set; }

            public int Instances { get; set; }

            public override string ToString()
            {
                return $"{Display} ({Instances})";
            }

            #endregion Properties
        }

        #endregion Classes

        #region Methods

        public override string Display(int Value)
        {
            return Values[Value];
        }

        public override void Seed(string input, bool PositiveIndicator)
        {
            if (!valuesDict.ContainsKey(input))
            {
                EnumOption option = new()
                {
                    Display = input,
                    Instances = 1,
                    Indicators = PositiveIndicator ? 1 : 0,
                    Index = valuesDict.Count + 1
                };

                lock (valuesDict)
                {
                    valuesDict.Add(input, option);
                }
            }
            else
            {
                EnumOption option = valuesDict[input];

                if (PositiveIndicator)
                {
                    option.Indicators++;
                }

                option.Instances++;
            }
        }

        public override int Transform(string input)
        {
            for (int i = 1; i < Values.Length; i++)
            {
                if (string.Equals(Values[i], input, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        protected override void OnDispose()
        {
            valuesDict.Clear();
        }

        public override void EndSeed()
        {
            List<EnumOption> options = new();
            EnumOption Other = new()
            {
                Display = "@OTHER"
            };

            foreach (KeyValuePair<string, EnumOption> kvp in valuesDict)
            {
                EnumOption thisOption = kvp.Value;

                if (thisOption.Instances > GlobalSettings.MinimumInstances)
                {
                    options.Add(thisOption);
                }
                else
                {
                    Other.Instances++;
                }
            }

            List<EnumOption> ParsedOptions = new()
            {
                Other.Instances > GlobalSettings.MinimumInstances ? Other : null
            };

            foreach (EnumOption option in options.OrderByDescending(e => e.Instances))
            {
                ParsedOptions.Add(option);
            }

            Values = ParsedOptions.Select(s => s?.Display).ToArray();

            #endregion Methods
        }
    }
}