using System.Collections.Specialized;
using System.Net;

public interface IHttpCommandHandler {
    /// <summary>
    /// HTTPリクエストを処理する。
    /// </summary>
    /// <param name="context">HttpListenerContext</param>
    /// <param name="query">クエリパラメータ</param>
    void HandleCommand(HttpListenerContext context, NameValueCollection query);
}
