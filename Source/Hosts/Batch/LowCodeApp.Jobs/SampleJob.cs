using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Repository.Match;
using LowCodeApp.Jobs.Services;

namespace LowCodeApp.Jobs
{
    /// <summary>
    /// 例: LowCodeApp.Batch Sample &lt;モジュール名&gt;
    /// 指定したモジュールの一覧を 1 ページ読んで、件数と先頭の Id を出す。
    /// </summary>
    public class SampleJob : IJob
    {
        public string Name => "Sample";

        public async Task RunAsync(DataService data, string[] args)
        {
            if (args.Length < 1) throw new ArgumentException("モジュール名を指定してください。例: Sample Customer");

            var moduleName = args[0];
            var page = await data.ModuleDataIO.GetListAsync(new SearchCondition(moduleName), 0);

            Console.WriteLine($"{moduleName}: {page.TotalCount} 件");
            foreach (var row in page.Items.Take(5))
            {
                Console.WriteLine($"  Id = {GetId(row)}");
            }
        }

        static string GetId(ModuleData row)
            => row.Fields.TryGetValue(SystemFieldNames.Id, out var id) && id is IdFieldData idData ? idData.Value ?? string.Empty : string.Empty;
    }
}
