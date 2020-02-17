using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Enumeration : BaseColumn
    {
        #region Fields

        public Dictionary<string, EnumOption> valuesDict = new Dictionary<string, EnumOption>();

        public Enumeration(DataSourceBuilder sourceBuilder) : base(sourceBuilder)
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

            #endregion Properties
        }

        #endregion Classes

        #region Methods

        public override string Display(int Value)
        {
            int i = 0;
            EnumOption e;

            //Im too lazy to make this thread safe and dictionary order isn't guaranteed so
            //since the index is the count we give that a check and just loop through if its wrong;
            lock (this.valuesDict)
            {
                e = this.valuesDict.ElementAt(Value).Value;
            }

            do
            {
                if (e.Index == Value)
                {
                    return e.Display;
                }

                lock (this.valuesDict)
                {
                    e = this.valuesDict.ElementAt(i).Value;
                }

                i++;
            } while (i < this.valuesDict.Count);

            return Value.ToString();
        }

        public override IEnumerable<int> GetOptions()
        {
            foreach (KeyValuePair<string, EnumOption> kvp in this.valuesDict)
            {
                EnumOption thisOption = kvp.Value;

                bool show = true;

                show = show && thisOption.Instances >= GlobalSettings.MinimumInstances;

                show = show && (!this.SourceBuilder.Settings.Results.MatchOnly || thisOption.Indicators > 0);

                if (show)
                {
                    yield return kvp.Value.Index;
                }
            }
        }

        public override int Transform(string input, bool PositiveIndicator)
        {
            if (!this.valuesDict.ContainsKey(input))
            {
                EnumOption option = new EnumOption()
                {
                    Display = input,
                    Instances = 1,
                    Indicators = PositiveIndicator ? 1 : 0,
                    Index = this.valuesDict.Count
                };

                lock (this.valuesDict)
                {
                    this.valuesDict.Add(input, option);
                }

                return this.valuesDict.Count - 1;
            }
            else
            {
                EnumOption option = this.valuesDict[input];

                if (PositiveIndicator)
                {
                    option.Indicators++;
                }

                option.Instances++;

                return option.Index;
            }
        }

        protected override void OnDispose()
        {
            this.valuesDict.Clear();
        }

        #endregion Methods
    }
}