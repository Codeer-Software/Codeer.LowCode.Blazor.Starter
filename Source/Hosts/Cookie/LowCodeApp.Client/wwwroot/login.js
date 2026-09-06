//ログイン画面のエンジン。login.html は枠と見た目だけを持ち、流れ (ID/パスワード → 二要素認証、外部 IdP のボタン、
//失敗理由の表示、antiforgery トークン) はここが担う。何を出すかはサーバーの api/account/login_options が返す
//(パスワードログインの有無・外部 IdP の一覧・デザインの LoginPage 設定 (タイトル / ロゴ / 案内文))。
//ブランディングは login.html (と AppSettings の LoginPage) で行い、このファイルは通常触らない。
(function () {
  "use strict";

  //画面の文言 (既定は日本語。login.html で window.loginTexts を定義すると上書きできる)
  const texts = Object.assign({
    title: "ログイン",
    loginFailed: "ログインに失敗しました",
    enterCode: "認証コードを入力してください",
    invalidCode: "認証コードが正しくありません",
    loginWith: "{0} でログイン",
    errors: {
      remote_failure: "ログインが中断されたか、失敗しました",
      invalid_claims: "アカウントを確認できませんでした",
      guest_not_allowed: "ゲストアカウントは使えません",
      domain_not_allowed: "このアカウントのドメインは許可されていません",
      user_not_registered: "このアカウントは登録されていません"
    }
  }, window.loginTexts || {});

  const params = new URLSearchParams(location.search);
  const redirect = sanitizeReturnUrl(params.get("returnUrl"));
  //二要素認証の 2 段階目に持ち越す 1 段階目の入力
  let pending = null;

  const el = id => document.getElementById(id);

  if (window.__blazorStarted) location.reload();

  //外部 IdP ログインに失敗したときサーバーが ?error= に付けて戻す理由コード
  const error = params.get("error");
  if (error) showError(texts.errors[error] ?? texts.loginFailed);

  (async function init() {
    let options = null;
    try {
      const res = await fetch("/api/account/login_options", { credentials: "include", cache: "no-store" });
      if (res.ok) options = await res.json();
    } catch { }
    options = options || {};
    const password = options.password ?? options.Password ?? true;
    const providers = options.providers ?? options.Providers ?? [];
    const page = options.page ?? options.Page ?? {};

    //デザインの LoginPage 設定: タイトル / ロゴ / 案内文
    const title = page.title || page.Title || texts.title;
    document.title = title;
    if (el("Title")) el("Title").textContent = title;
    const logoUrl = page.logoUrl || page.LogoUrl || "";
    if (el("Logo")) {
      if (logoUrl) { el("Logo").src = logoUrl; el("Logo").style.display = ""; }
      else el("Logo").style.display = "none";
    }
    const message = page.message || page.Message || "";
    if (el("Message")) {
      el("Message").textContent = message;
      el("Message").style.display = message ? "" : "none";
    }

    if (!password) el("LoginForm").style.display = "none";

    //パスワードログイン無効で IdP が 1 つなら画面を出さずに即遷移 (エラー表示中は止まるのでループしない)
    if (!password && providers.length === 1 && !error) {
      location.replace(providerUrl(providers[0]));
      return;
    }

    const container = el("Providers");
    for (const p of providers) {
      const a = document.createElement("a");
      a.className = "btn btn-outline-primary";
      a.href = providerUrl(p);
      a.textContent = texts.loginWith.replace("{0}", p.displayName ?? p.DisplayName ?? p.name ?? p.Name);
      container.appendChild(a);
    }
    document.body.classList.add("ready");
  })();

  function providerUrl(p) {
    const name = p.name ?? p.Name;
    return `/api/account/login/${encodeURIComponent(name)}?returnUrl=${encodeURIComponent(redirect)}`;
  }

  el("LoginForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const id = el("Id").value;
    const password = el("Password").value;
    const isPersistent = el("IsPersistent").checked;
    if (!id || !password) { showError(); return; }

    const btn = el("LoginButton");
    btn.disabled = true;
    hideError();
    try {
      const res = await postLogin({ Id: id, Password: password, IsPersistent: isPersistent });
      if (!res.ok) { showError(); return; }

      //二要素認証が有効なら status が setup / totp で返り、まだサインインしていない
      const j = await readJson(res);
      const status = j?.status ?? j?.Status ?? "ok";
      if (status === "setup" || status === "totp") {
        pending = { Id: id, Password: password, IsPersistent: isPersistent };
        if (status === "setup") {
          el("QrImage").src = "data:image/png;base64," + (j?.qrPngBase64 ?? j?.QrPngBase64 ?? "");
          el("SecretText").textContent = j?.secret ?? j?.Secret ?? "";
        }
        showTotp(status === "setup");
        return;
      }
      if (status !== "ok") { showError(); return; }
      await enterApp();
    } catch {
      showError();
    } finally {
      btn.disabled = false;
    }
  });

  el("TotpForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    if (!pending) { backToLogin(); return; }
    const code = el("TotpCode").value.trim();
    if (!code) { showError(texts.enterCode); return; }

    const btn = el("VerifyButton");
    btn.disabled = true;
    hideError();
    try {
      const res = await postLogin({ ...pending, TotpCode: code });
      if (!res.ok) { showError(); return; }
      const j = await readJson(res);
      const status = j?.status ?? j?.Status ?? "ok";
      if (status !== "ok") { showError(texts.invalidCode); return; }
      await enterApp();
    } catch {
      showError();
    } finally {
      btn.disabled = false;
    }
  });

  el("BackButton").addEventListener("click", backToLogin);

  async function postLogin(body) {
    const token = await getAntiforgeryToken();
    return await fetch("/api/account/login", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-ANTIFORGERY-TOKEN": token },
      body: JSON.stringify(body),
      credentials: "include",
      cache: "no-store"
    });
  }
  async function readJson(res) {
    try { return await res.json(); } catch { return null; }
  }
  //サインイン後: Cookie でユーザーが取れることを確かめてからアプリへ
  async function enterApp() {
    const me = await fetch("/api/account/current_user", { credentials: "include", cache: "no-store" });
    if (me.ok) {
      const j = await me.json();
      const uid = (j?.value ?? j?.Value ?? "").trim();
      if (uid) { location.href = redirect === "" ? "/" : redirect; return; }
    }
    showError();
  }
  function showTotp(isSetup) {
    el("LoginForm").style.display = "none";
    el("Providers").style.display = "none";
    el("TotpForm").style.display = "";
    el("TotpSetup").style.display = isSetup ? "" : "none";
    el("TotpPrompt").style.display = isSetup ? "none" : "";
    el("TotpCode").value = "";
    el("TotpCode").focus();
  }
  function backToLogin() {
    pending = null;
    hideError();
    el("TotpForm").style.display = "none";
    el("LoginForm").style.display = "";
    el("Providers").style.display = "";
    el("Password").value = "";
  }
  async function getAntiforgeryToken() {
    try { await fetch("/api/account/antiforgery", { credentials: "include", cache: "no-store" }); } catch { }
    return document.cookie.split("; ").find(x => x.startsWith("X-ANTIFORGERY-TOKEN="))?.split("=")[1] ?? "";
  }
  function showError(message) {
    const e = el("ErrorMessage");
    e.textContent = message ?? texts.loginFailed;
    e.style.display = "block";
  }
  function hideError() {
    el("ErrorMessage").style.display = "none";
  }
  function sanitizeReturnUrl(u) {
    if (!u || !u.startsWith("/") || u.startsWith("//")) return "/";
    return u;
  }
})();
