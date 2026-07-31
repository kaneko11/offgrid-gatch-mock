using System;
using System.Collections;
using System.Collections.Generic;

namespace ExternalLib
{
    /// <summary>Sample DTO defined in the (shared) external assembly, used as a method-shim element type.</summary>
    public class GatewayItem
    {
        public GatewayItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>
    /// Sample external gateway whose instance methods are intercepted by the Phase 25 method-shim feature.
    /// The "real" implementations represent expensive/IO behaviour that must NOT run when shimmed
    /// (Query/RawQuery throw so a successful test proves the shim replaced the call).
    /// </summary>
    public class ExternalGateway
    {
        // 非 virtual インスタンスメソッド
        public string GetName(int id)
        {
            return "real-" + id;
        }

        // ジェネリックインスタンスメソッド（戻り値が IEnumerable<T>）
        public IEnumerable<T> Query<T>(string sql)
        {
            throw new NotSupportedException("real Query must not be called when shimmed.");
        }

        // 戻り値の差し替え検証用: 内部 ctor の具象型を返すが、呼び出し側は IEnumerable<T> として消費する
        // （EF の DbRawSqlQuery<T> 相当の状況を汎用サンプルで再現）
        public RawResult<T> RawQuery<T>(string sql)
        {
            throw new NotSupportedException("real RawQuery must not be called when shimmed.");
        }
    }

    /// <summary>
    /// Generic Phase 25 sample with an int-returning, non-virtual instance method. The optional
    /// parameter remains part of the three-parameter runtime signature.
    /// </summary>
    public class ExternalTableLoader
    {
        public int Load(object combo, string sql, bool setAll = true)
        {
            throw new InvalidOperationException("Real database access");
        }

        public int Load(string name)
        {
            return name.Length;
        }

        public int NoArguments()
        {
            return 11;
        }

        public virtual int VirtualLoad(int value)
        {
            return value + 1;
        }

        public static int StaticLoad(string sql)
        {
            return sql.Length;
        }
    }

    /// <summary>
    /// IEnumerable&lt;T&gt; implementation whose constructor is <c>internal</c> (test code cannot create it),
    /// mimicking Entity Framework's <c>DbRawSqlQuery&lt;T&gt;</c>.  Exercises return-type substitution.
    /// </summary>
    public class RawResult<T> : IEnumerable<T>
    {
        internal RawResult()
        {
        }

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }
}
