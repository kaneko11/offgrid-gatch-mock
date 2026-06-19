using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 24 — rewritten object inspection API.
/// Verifies that the object graph produced by a <c>ForAssembly</c> session can be observed by property
/// path / collection wrappers without casting rewritten types to the test's original types.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InspectionApiTests
{
    private const string ViewModelTypeName = "CrossAssemblySample.UserViewModel";
    private const string ExternalDbTypeName = "ExternalLib.ExternalDbContext";

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.UserViewModel).Assembly.Location;

    private static string ExternalAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "ExternalLib.dll");

    private sealed class FakeExternalDbContext : ExternalDbContext
    {
        public override string GetName(int id) => "fake-" + id;
    }

    private static Shims CreateSessionWithFake() =>
        Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext());

    [TestMethod]
    public void GetValue_NestedProperty()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            Assert.AreEqual("fake-1", shims.GetValue<string>(vm, "SelectedUser.Name"));
        }
    }

    [TestMethod]
    public void GetValue_CollectionCount()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            Assert.AreEqual(1, shims.GetValue<int>(vm, "Items.Count"));
        }
    }

    [TestMethod]
    public void GetValue_CollectionItemProperty()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            Assert.AreEqual("fake-1", shims.GetValue<string>(vm, "Items[0].Name"));
        }
    }

    [TestMethod]
    public void GetCollection_ReturnsShimsCollection_WithItemAccess()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            var items = shims.GetCollection(vm, "Items");

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("fake-1", items[0].Get<string>("Name"));
        }
    }

    [TestMethod]
    public void ObservableCollection_MultipleRewrittenItems_AreInspectable()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "LoadMany");

            var items = shims.GetCollection(vm, "Items");

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("fake-1", items[0].GetValue<string>("Name"));
            Assert.AreEqual("fake-2", items[1].GetValue<string>("Name"));
        }
    }

    [TestMethod]
    public void ShimsObject_NestedAndCollectionAccess()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            var root = shims.Inspect(vm);

            Assert.AreEqual("fake-1", root.GetObject("SelectedUser").GetValue<string>("Name"));
            Assert.AreEqual("fake-1", root.GetCollection("Items")[0].GetValue<string>("Name"));
        }
    }

    [TestMethod]
    public void GetValueObject_ReturnsRawObject()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            var raw = shims.GetValue<object>(vm, "SelectedUser");

            Assert.IsNotNull(raw);
            Assert.AreEqual("UserItem", raw.GetType().Name);
        }
    }

    [TestMethod]
    public void StronglyTypedCast_Unsafe_ButInspectionWorks()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            // The rewritten UserItem has a different load-context identity than the test's reference,
            // so a strongly typed conversion must be refused with guidance (not InvalidCastException).
            var ex = Assert.ThrowsException<ShimsInspectionException>(
                () => shims.GetValue<CrossAssemblySample.UserItem>(vm, "SelectedUser"));
            StringAssert.Contains(ex.Message, "different load context");

            // ...but inspecting the value's primitive property still works.
            Assert.AreEqual("fake-1", shims.GetValue<string>(vm, "SelectedUser.Name"));
        }
    }

    [TestMethod]
    public void NullInPath_ThrowsClearException()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            // No Load() -> SelectedUser is null.

            var ex = Assert.ThrowsException<ShimsInspectionException>(
                () => shims.GetValue<string>(vm, "SelectedUser.Name"));

            StringAssert.Contains(ex.Message, "null was encountered");
            StringAssert.Contains(ex.Message, "SelectedUser.Name");
            StringAssert.Contains(ex.Message, "SelectedUser");
        }
    }

    [TestMethod]
    public void MissingProperty_ThrowsClearException()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            var ex = Assert.ThrowsException<ShimsInspectionException>(
                () => shims.GetValue<string>(vm, "DoesNotExist"));

            StringAssert.Contains(ex.Message, "DoesNotExist");
            StringAssert.Contains(ex.Message, "Target runtime type:");
            StringAssert.Contains(ex.Message, "Reason:");
        }
    }

    [TestMethod]
    public void IndexOutOfRange_ThrowsClearException()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load"); // count == 1

            var ex = Assert.ThrowsException<ShimsInspectionException>(
                () => shims.GetValue<string>(vm, "Items[5].Name"));

            StringAssert.Contains(ex.Message, "index out of range");
            StringAssert.Contains(ex.Message, "Items[5]");
        }
    }

    [TestMethod]
    public void GetProperty_ReadsSingleMember()
    {
        using (var shims = CreateSessionWithFake())
        {
            var vm = shims.CreateObject(ViewModelTypeName);
            shims.Invoke(vm, "Load");

            var item = shims.Inspect(vm).GetCollection("Items")[0];
            Assert.AreEqual("fake-1", item.GetProperty<string>("Name"));
        }
    }
}
