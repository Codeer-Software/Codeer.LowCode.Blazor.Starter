using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.DesignLogic.Transfer;
using Codeer.LowCode.Blazor.Repository.Data;
using System.Collections.Concurrent;
using LowCodeApp.Client.Shared.Services;
using Codeer.LowCode.Blazor.DbAccess;

namespace LowCodeApp.Server.Services
{
    static class DesignerService
    {
        static object _sync = new();
        static Dictionary<string, DesignData> _designDataCacheDictionary = new();
        static Dictionary<string, TransferDesignData> _transferDataDictionary = new();
        static ConcurrentDictionary<string, bool> _resourceTenant = new();

        internal static DesignData GetDesignData(string tenantKey)
        {
            GetDesignData(tenantKey, out var designData, out _);
            return designData;
        }

        internal static void GetDesignData(string tenantKey, out DesignData designData, out TransferDesignData transferDesignData)
        {
            lock (_sync)
            {
                if (!_designDataCacheDictionary.TryGetValue(tenantKey, out var src)) src = null;
                
                var subZipFileDirectory = Path.Combine(SystemConfig.Instance.DesignFileDirectory, tenantKey);
                designData = DesignDataFileManager.GetDesignDataForMultiTenant(tenantKey, subZipFileDirectory, _designDataCacheDictionary);

                if (ReferenceEquals(src, designData) && _transferDataDictionary.ContainsKey(tenantKey))
                {
                    transferDesignData = _transferDataDictionary[tenantKey];
                    return;
                }
                DbAccessor.ClearTableDefinitionCache();
                transferDesignData = designData.CreateTransferDesignData();
               _transferDataDictionary[tenantKey] = transferDesignData;
            }
        }

        internal static byte[] GetDesignDataForFront(string tenantKey, ModuleData? currentUser)
        {
            GetDesignData(tenantKey, out var designData, out var transferDesignData);
            return transferDesignData.AddResolvedPageFrames(designData.ResolvePageFrames(new PageLinkUrlResolver(), currentUser)).ToBinary();
        }

        internal static MemoryStream? GetResource(string tenantKey, string resourcePath)
        {
            var resourceKey = tenantKey + "|" + resourcePath;
            var tenantPath = Path.Combine(SystemConfig.Instance.DesignFileDirectory, tenantKey);
            var mainPath = Path.Combine(SystemConfig.Instance.DesignFileDirectory, "Main");

            if (!_resourceTenant.TryGetValue(resourceKey, out var existTenant))
            {
                var mem = DesignDataFileManager.GetResource(tenantPath, resourcePath);
                if (mem != null)
                {
                    _resourceTenant[resourceKey] = true;
                    return mem;
                }
                _resourceTenant[resourceKey] = false;
                return DesignDataFileManager.GetResource(mainPath, resourcePath);
            }
            return DesignDataFileManager.GetResource(existTenant ? tenantPath : mainPath, resourcePath);
        }
    }
}
