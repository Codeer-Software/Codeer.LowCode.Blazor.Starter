using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.Design;
using Codeer.LowCode.Blazor.Repository.Data;
using LowCodeApp.Client.Shared.Services;

namespace LowCodeApp.Server.Infrastructure
{
    /// <summary>
    /// このリクエストが使うデザイン。最初に参照した時点の版に固定する (リクエストの中では AppDesign.Store でなくこれを使う)。
    /// </summary>
    public class RequestDesign
    {
        readonly LoadedDesign _design = AppDesign.Store.GetCurrent();

        public DesignData DesignData => _design.DesignData;

        public string Version => _design.Version;

        //フロントへ渡すデザイン (ユーザーごとのページフレームの解決つき)
        public byte[] ForFront(ModuleData? currentUser) => _design.ForFront(new PageLinkUrlResolver(), currentUser);
    }
}
