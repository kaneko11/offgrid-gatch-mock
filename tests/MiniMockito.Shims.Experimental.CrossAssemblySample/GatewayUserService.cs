using System.Collections.Generic;
using System.Linq;
using ExternalLib;

namespace CrossAssemblySample
{
    /// <summary>
    /// Sample caller whose method bodies invoke <see cref="ExternalGateway"/> instance methods.
    /// These call sites are the target of the Phase 25 method-shim rewrite.
    /// </summary>
    public class GatewayUserService
    {
        // 非 virtual メソッド呼び出し
        public string Run(int id)
        {
            var gateway = new ExternalGateway();
            return gateway.GetName(id);
        }

        // ジェネリックメソッド呼び出し（IEnumerable<T> として即消費）
        public List<GatewayItem> LoadRows()
        {
            var gateway = new ExternalGateway();
            return gateway.Query<GatewayItem>("select ...").ToList();
        }

        // 戻り値型差し替え（RawResult<T> を IEnumerable<T> として即消費）
        public List<GatewayItem> LoadRawRows()
        {
            var gateway = new ExternalGateway();
            return gateway.RawQuery<GatewayItem>("select ...").ToList();
        }
    }
}
