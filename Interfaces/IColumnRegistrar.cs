using Penguin.Analysis.DataColumns;
using Penguin.Analysis.Transformations;

namespace Penguin.Analysis.Interfaces
{
    public interface IColumnRegistrar
    {
        void RegisterColumn(string ColumnName, IDataColumn registration);

        void RegisterColumn<T>(params string[] ColumnNames) where T : IDataColumn;

        void RegisterTransformation(ITransform transform);
    }
}