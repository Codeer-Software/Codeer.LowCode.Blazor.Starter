namespace LowCodeApp.Client
{
    public class LoginInfo
    {
        public string? Id { get; set; }
        public string? Password { get; set; }
        public bool IsPersistent { get; set; }
        //二要素認証 (TOTP) が有効なときの 2 段階目。オーセンティケータの 6 桁コード
        public string? TotpCode { get; set; }
    }

    //ネイティブアプリ (MAUI) 用: 外部 IdP ログイン後に受け取る使い捨てチケット。認証 Cookie と交換する
    public class LoginTicket
    {
        public string? Ticket { get; set; }
    }

    //POST api/account/logout の結果。IdP 側のセッションも終わらせる必要があるときは Redirect に遷移先が入る
    public class LogoutResult
    {
        public string? Redirect { get; set; }
    }
}
