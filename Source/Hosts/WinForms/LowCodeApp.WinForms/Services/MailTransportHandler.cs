using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Mail;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.Extras.Server.Mail;

namespace LowCodeApp.WinForms.Services
{
    /// <summary>
    /// デスクトップ用のメール経路。HTTP (MailController) の代わりに MailDispatcher / MailBulkSearch / MailPreviewBuilder を直接呼ぶ
    /// (MailTransport.Handler に設定する)。処理ごとに DB 接続を開いて閉じる (ModuleDataService と同じ流儀)。
    /// </summary>
    public class MailTransportHandler : IMailTransportHandler
    {
        public async Task<MailSendResult> SendAsync(MailSendRequest request)
            => await RunAsync(async (dispatcher, _) => await dispatcher.SendAsync(request));

        public async Task<MailSendResult> SendBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, io) => await new MailBulkSearch(dispatcher, io, DesignerService.GetDesignData(), LogError).SendAsync(request));

        public async Task<byte[]?> PreviewAsync(MailPreviewRequest request)
            => await RunAsync(async (dispatcher, io) => ToBytes(await new MailPreviewBuilder(dispatcher, io, DesignerService.GetDesignData()).BuildSingleHtmlAsync(request)));

        public async Task<byte[]?> PreviewBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, io) => ToBytes(await new MailPreviewBuilder(dispatcher, io, DesignerService.GetDesignData()).BuildBulkHtmlAsync(request)));

        static byte[] ToBytes(string html) => System.Text.Encoding.UTF8.GetBytes(html);
        static void LogError(string message) => System.Diagnostics.Debug.WriteLine(message);

        static async Task<T> RunAsync<T>(Func<MailDispatcher, CustomizedModuleDataIO, Task<T>> action)
        {
            await using var dbAccess = new DbAccessor(SystemConfig.Instance.DataSources);
            var temporaryFileManager = new TemporaryFileManager(dbAccess, SystemConfig.Instance.TemporaryFileTableInfo, SystemConfig.Instance.FileStorages);
            var io = new CustomizedModuleDataIO(DesignerService.GetDesignData(), new AuthenticationContext(), dbAccess, temporaryFileManager);

            var mail = SystemConfig.Instance.Mail;
            //履歴はシステムの記録なので、操作ユーザーの書き込み権限に依存しない内部経路で書く
            var historyWriter = string.IsNullOrEmpty(mail.HistoryModuleName)
                ? null
                : new MailHistoryWriter(mail.HistoryModuleName, DesignerService.GetDesignData(), data => io.AddSystemRecordAsync(data), LogError);
            var dispatcher = new MailDispatcher(mail,
                name => name switch
                {
                    //呼び名→送信インフラの対応表 (独自インフラはここに足す)
                    "Gmail" => new GmailApiMailSender(SystemConfig.Instance.Gmail),
                    _ => null,
                },
                historyWriter);
            return await action(dispatcher, io);
        }
    }
}
