using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DbAccess;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace LowCodeApp.Server.Infrastructure
{
    /// <summary>
    /// 例外を利用者に返すときの方針。本体の例外 (LowCodeException。検証・楽観ロック・権限などの利用者向けの文言) はそのまま、
    /// 一意制約違反は利用者向けの文言に、それ以外 (DB の例外など。表名や値を含む) は開発時だけ原文、本番は固定の文言 + リクエスト Id (原文はログで引く)。
    /// </summary>
    public static class ExceptionHandlerUtils
    {
        //未処理の例外 (一覧の読み出しなど)。原文は UseExceptionHandler がログに出す
        public static void UseExceptionHandlerSendToFront(this WebApplication app)
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var ex = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
                    if (ex == null) return;
                    context.Response.ContentType = "text/plain";
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    await context.Response.WriteAsync(ToClientMessage(ex, context));
                });
            });
        }

        //保存の結果 (ModuleSubmitResult.ExceptionMessage)。保存の失敗は例外にならないので原文はここでログに出す
        public static List<ModuleSubmitResult> ToClientMessage(this List<ModuleSubmitResult> results, HttpContext context)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ExceptionHandlerUtils));
            foreach (var ex in results.Select(e => e.Exception).OfType<Exception>().Distinct().Where(e => !IsUserFacing(e)))
            {
                logger.LogError(ex, "Submit failed. TraceIdentifier={TraceIdentifier}", context.TraceIdentifier);
            }
            foreach (var result in results.Where(e => e.Exception != null))
            {
                result.ExceptionMessage = ToClientMessage(result.Exception!, context);
            }
            return results;
        }

        //利用者の操作で普通に起きる失敗 (ログに残すものではない)
        static bool IsUserFacing(Exception ex) => ex is LowCodeException || DbErrors.IsUniqueViolation(ex);

        static string ToClientMessage(Exception ex, HttpContext context)
        {
            if (ex is LowCodeException) return ex.Message;
            if (DbErrors.IsUniqueViolation(ex)) return "同じ値のデータが既にあります。";
            if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()) return ex.GetMessages();
            return $"サーバーでエラーが発生しました。(Id: {context.TraceIdentifier})";
        }

        static string GetMessages(this Exception? ex)
        {
            var list = new List<string>();
            while (ex != null)
            {
                list.Add(ex.Message);
                ex = ex.InnerException;
            }
            return string.Join(Environment.NewLine, list);
        }
    }
}
