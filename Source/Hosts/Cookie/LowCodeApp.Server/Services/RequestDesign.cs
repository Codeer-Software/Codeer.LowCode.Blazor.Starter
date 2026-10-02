using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;

namespace LowCodeApp.Server.Services
{
    /// <summary>
    /// このリクエストが使うデザイン (スコープ)。最初に参照した時点の版に固定し、途中で App.zip が差し替わっても最後まで同じ版で動く。
    /// リクエストの中では DesignerService から直接取らず、これ (DataService.Design) を使う。
    /// </summary>
    public class RequestDesign
    {
        readonly LoadedDesign _design = DesignerService.GetCurrent();

        public DesignData DesignData => _design.DesignData;

        public string Version => _design.Version;

        //フロントへ渡すデザイン (ユーザーごとのページフレームの解決つき)
        public byte[] ForFront(ModuleData? currentUser) => _design.ForFront(currentUser);
    }
}
