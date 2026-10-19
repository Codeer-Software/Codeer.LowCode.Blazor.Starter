using Codeer.LowCode.Blazor.Extras.Mail;
using Codeer.LowCode.Blazor.Extras.Server.Mail;

namespace LowCodeApp.Jobs.Services
{
    /// <summary>
    /// バッチ用のメール経路 (HTTP の代わりにメール処理を直接呼ぶ)。
    /// </summary>
    public class MailTransportHandler : IMailTransportHandler
    {
        readonly string _userId;

        public MailTransportHandler(string userId)
        {
            _userId = userId;
        }

        public async Task<MailSendResult> SendAsync(MailSendRequest request)
            => await RunAsync(async (dispatcher, data) => await dispatcher.SendAsync(request, data.ModuleDataIO));

        public async Task<MailSendResult> SendBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, data) => await new MailBulkSearch(dispatcher, data.ModuleDataIO, data.Design, LogError).SendAsync(request));

        public async Task<byte[]?> PreviewAsync(MailPreviewRequest request)
            => await RunAsync(async (dispatcher, data) => ToBytes(await new MailPreviewBuilder(dispatcher, data.ModuleDataIO, data.Design).BuildSingleHtmlAsync(request)));

        public async Task<byte[]?> PreviewBulkSearchAsync(MailBulkSearchRequest request)
            => await RunAsync(async (dispatcher, data) => ToBytes(await new MailPreviewBuilder(dispatcher, data.ModuleDataIO, data.Design).BuildBulkHtmlAsync(request)));

        static byte[] ToBytes(string html) => System.Text.Encoding.UTF8.GetBytes(html);
        static void LogError(string message) => Console.Error.WriteLine(message);

        async Task<T> RunAsync<T>(Func<MailDispatcher, DataService, Task<T>> action)
        {
            await using var data = new DataService(_userId);
            var mail = SystemConfig.Instance.Mail;
            //履歴はシステムの記録なので、操作ユーザーの書き込み権限に依存しない内部経路で書く
            var historyWriter = string.IsNullOrEmpty(mail.HistoryModuleName)
                ? null
                : new MailHistoryWriter(mail.HistoryModuleName, data.Design, e => data.ModuleDataIO.AddSystemRecordAsync(e), LogError);
            //呼び名→送信インフラの対応表は MailSenderTable (独自インフラはそこに足す)
            var dispatcher = new MailDispatcher(mail, MailSenderTable.Create, historyWriter);
            return await action(dispatcher, data);
        }
    }
}
