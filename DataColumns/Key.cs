using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Key : Bool
    {
        public Key(DataSourceBuilder sourceBuilder) : base(sourceBuilder)
        {
        }
    }
}