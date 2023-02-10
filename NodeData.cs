using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    public class NodeData
    {
        public Accuracy Accuracy { get; set; }

        public Dictionary<string, object> Data { get; }

        public INode Node { get; set; }

        public double Score { get; set; }

        public double Weight { get; set; }

        public NodeData(INode node, DataSourceBuilder builder, Evaluation e = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            double ColumnInstances = e != null ? e.Scores[node.Key].ColumnInstances : builder.Result.ColumnInstances[node.Key];
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Score = Node.GetScore(builder.Result.BaseRate);
            Weight = builder.Result.GraphInstances / ColumnInstances;
            LongByte Key = new(Node.Key);

            Data = new Dictionary<string, object>()
            {
                ["Accuracy"] = node.Accuracy.Current,
                ["Next Accuracy"] = node.Accuracy.Next,
                ["Weight"] = Weight,
                ["Score"] = Score,
                ["Weighted Score"] = Score * Weight,
                ["GraphInstances"] = builder.Result.GraphInstances,
                ["ColumnInstances"] = ColumnInstances,
                ["Match None"] = node[MatchResult.None],
                ["Match Route"] = node[MatchResult.Route],
                ["Match Predictor"] = node[MatchResult.Output],
                ["Match Both"] = node[MatchResult.Both],
                ["Key"] = new LongByte(Node.Key),
                ["Name"] = builder.GetNodeName(node)
            };

            if (e != null)
            {
                int h = 0;
                foreach (string s in e.CalculatedData.Keys)
                {
                    Data.Add(s, Key.HasBitAt(h++) ? e.CalculatedData[s] : "");
                }
            }

            int i = 0;
            foreach (string s in builder.Registrations.Select(r => r.Header))
            {
                Data.Add($"{s}_Bit", Key.HasBitAt(i++) ? 1 : 0);
            }
        }
    }
}