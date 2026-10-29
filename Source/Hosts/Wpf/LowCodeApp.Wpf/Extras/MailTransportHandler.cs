using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Mail;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.Extras.Server.Mail;

using LowCodeApp.Wpf.Services;

namespace LowCodeApp.Wpf.Extras
{
    /// <summary>
    /// デスクトップ用のメール経路 (HTTP の代わりにメール処理を直接呼ぶ)。
    /// </summary>
    public class MailTransportHandler : IMailTransportHandler
    {
        public async Task<MailSendResult> SendAsync(MailSendRequest request)
            => await RunAsync(async (dispatcher, io) => await dispatcher.SendAsync(request, io));

        public async Task<MailSendResult> SendBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, io) => await new MailBulkSearch(dispatcher, io, AppDesign.Store.GetDesignData(), LogError).SendAsync(request));

        public async Task<byte[]?> PreviewAsync(MailPreviewRequest request)
            => await RunAsync(async (dispatcher, io) => ToBytes(await new MailPreviewBuilder(dispatcher, io, AppDesign.Store.GetDesignData()).BuildSingleHtmlAsync(request)));

        public async Task<byte[]?> PreviewBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, io) => ToBytes(await new MailPreviewBuilder(dispatcher, io, AppDesign.Store.GetDesignData()).BuildBulkHtmlAsync(request)));

        static byte[] ToBytes(string html) => System.Text.Encoding.UTF8.GetBytes(html);
        static void LogError(string message) => System.Diagnostics.Debug.WriteLine(message);

        static async Task<T> RunAsync<T>(Func<MailDispatcher, CustomizedModuleDataIO, Task<T>> action)
        {
            await using var dbAccess = new DbAccessor(SystemConfig.Instance.DataSources);
            var temporaryFileManager = new TemporaryFileManager(dbAccess, SystemConfig.Instance.TemporaryFileTableInfo, FileStorageTable.Storages);
            var io = new CustomizedModuleDataIO(AppDesign.Store.GetDesignData(), new AuthenticationContext(), dbAccess, temporaryFileManager);

            var mail = SystemConfig.Instance.Mail;
            var historyWriter = string.IsNullOrEmpty(mail.HistoryModuleName)
                ? null
                : new MailHistoryWriter(mail.HistoryModuleName, AppDesign.Store.GetDesignData(), data => io.AddSystemRecordAsync(data), LogError);
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
