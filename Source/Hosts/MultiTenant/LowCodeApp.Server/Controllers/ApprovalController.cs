using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Approval;
using Codeer.LowCode.Blazor.Extras.Server.Approval;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LowCodeApp.Server.Services;

namespace LowCodeApp.Server.Controllers
{
    //承認フローの API
    [Authorize, AutoValidateAntiforgeryToken]
    [ApiController]
    [Route("api/approval")]
    public class ApprovalController : ControllerBase, IAsyncDisposable
    {
        readonly DataService _dataService;
        readonly ILogger<ApprovalController> _logger;

        public ApprovalController(DataService dataService, ILogger<ApprovalController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
            => await _dataService.DisposeAsync();

        //すべての操作 (申請/再申請/承認/却下/差し戻し/取り下げ/確認) を 1 本で受け、ApprovalEngine が Action で振り分ける
        [HttpPost]
        public async Task<ApprovalActionResult> ExecuteAsync(ApprovalCommand command)
            => await CreateEngine().ExecuteAsync(command);

        ApprovalEngine CreateEngine()
        {
            var mail = SystemConfig.Instance.Mail;
            var historyWriter = string.IsNullOrEmpty(mail.HistoryModuleName)
                ? null
                : new MailHistoryWriter(mail.HistoryModuleName, DesignerService.GetDesignData(_dataService.TenantKey),
                    data => _dataService.ModuleDataIO.AddSystemRecordAsync(data), e => _logger.LogError("{Error}", e));
            return new(DesignerService.GetDesignData(_dataService.TenantKey), _dataService.ModuleDataIO, _dataService.DbAccess,
                data => _dataService.ModuleDataIO.AddSystemRecordAsync(data),
                data => _dataService.ModuleDataIO.UpdateSystemRecordAsync(data))
            {
                //順番到達の通知メール (メンバー契約の TurnNotifyMail が設定されているときだけ送られる)
                MailDispatcher = new MailDispatcher(mail, name => MailSenderTable.Create(name), historyWriter: historyWriter),
                LogError = e => _logger.LogError("{Error}", e),
            };
        }
    }
}
