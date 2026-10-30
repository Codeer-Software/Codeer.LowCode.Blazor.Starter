using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.Api;
using Codeer.LowCode.Blazor.Extras.Server.Approval;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Codeer.LowCode.Blazor.Extras.Server.Mail;

using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Extras
{
    /// <summary>
    /// Extras の機能の API (Controllers/ExtrasController.cs が受ける) に出す機能と、機能に渡すものの対応表。
    /// 機能を足すときはここに 1 行足す。並べていない機能の要求は 404。
    /// </summary>
    public static class ExtrasApiTable
    {
        public static ExtrasApi Create() => new()
        {
            Handlers =
            {
                //メール (MailField / BulkMailField / Mail スクリプトオブジェクト)。送信インフラの対応表は MailSenderTable
                new MailApi(SystemConfig.Instance.Mail, MailSenderTable.Create),
                //承認フロー (ApprovalFlowField)
                new ApprovalApi(SystemConfig.Instance.Mail, MailSenderTable.Create),
                //Excel → PDF (Excel スクリプトオブジェクトの DownloadPdf)
                new ExcelApi(),
                //AI 文書解析 (AITextAnalyzerField)
                new AITextAnalyzeApi(SystemConfig.Instance.AISettings),
            },
        };
    }
}
