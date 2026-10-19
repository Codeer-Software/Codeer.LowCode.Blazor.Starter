using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.DbAccess;

namespace LowCodeApp.Jobs.Services
{
    static class DesignerService
    {
        static object _sync = new();
        static DesignData _designData = new();

        //DesignFileDirectory の App.zip から読んだデザイン (App.zip が変わっていなければ前回のものを返す)
        internal static DesignData GetDesignData()
        {
            lock (_sync)
            {
                var designData = DesignDataFileManager.GetDesignData(SystemConfig.Instance.DesignFileDirectory, _designData);
                if (ReferenceEquals(designData, _designData)) return _designData;
                DbAccessor.ClearTableDefinitionCache();
                _designData = designData;
                return _designData;
            }
        }
    }
}
