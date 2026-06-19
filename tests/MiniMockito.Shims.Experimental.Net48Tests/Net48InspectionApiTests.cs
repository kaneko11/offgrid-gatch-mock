using System;
using System.IO;
using CrossAssemblySample;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 24 — rewritten object inspection API on .NET Framework 4.8 (C# 7.3, using-statement form).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48InspectionApiTests
    {
        private const string ViewModelTypeName = "CrossAssemblySample.UserViewModel";
        private const string ExternalDbTypeName = "ExternalLib.ExternalDbContext";

        private static string TargetAssemblyPath
        {
            get { return typeof(UserViewModel).Assembly.Location; }
        }

        private static string ExternalAssemblyPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalLib.dll"); }
        }

        private sealed class FakeExternalDbContext : ExternalDbContext
        {
            public override string GetName(int id)
            {
                return "fake-" + id;
            }
        }

        private static Shims CreateSessionWithFake()
        {
            return Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext());
        }

        [TestMethod]
        public void GetValue_NestedProperty()
        {
            using (Shims shims = CreateSessionWithFake())
            {
                object vm = shims.CreateObject(ViewModelTypeName);
                shims.Invoke(vm, "Load");

                Assert.AreEqual("fake-1", shims.GetValue<string>(vm, "SelectedUser.Name"));
            }
        }

        [TestMethod]
        public void GetValue_CollectionCountAndItem()
        {
            using (Shims shims = CreateSessionWithFake())
            {
                object vm = shims.CreateObject(ViewModelTypeName);
                shims.Invoke(vm, "Load");

                Assert.AreEqual(1, shims.GetValue<int>(vm, "Items.Count"));
                Assert.AreEqual("fake-1", shims.GetValue<string>(vm, "Items[0].Name"));
            }
        }

        [TestMethod]
        public void GetCollection_ObservableCollection_IsInspectable()
        {
            using (Shims shims = CreateSessionWithFake())
            {
                object vm = shims.CreateObject(ViewModelTypeName);
                shims.Invoke(vm, "LoadMany");

                ShimsCollection items = shims.GetCollection(vm, "Items");

                Assert.AreEqual(2, items.Count);
                Assert.AreEqual("fake-1", items[0].Get<string>("Name"));
                Assert.AreEqual("fake-2", items[1].Get<string>("Name"));
            }
        }

        [TestMethod]
        public void GetValueObject_ReturnsRawObject()
        {
            using (Shims shims = CreateSessionWithFake())
            {
                object vm = shims.CreateObject(ViewModelTypeName);
                shims.Invoke(vm, "Load");

                object raw = shims.GetValue<object>(vm, "SelectedUser");

                Assert.IsNotNull(raw);
                Assert.AreEqual("UserItem", raw.GetType().Name);
            }
        }

        [TestMethod]
        public void NullInPath_ThrowsClearException()
        {
            using (Shims shims = CreateSessionWithFake())
            {
                object vm = shims.CreateObject(ViewModelTypeName);

                ShimsInspectionException ex = Assert.ThrowsException<ShimsInspectionException>(
                    delegate { shims.GetValue<string>(vm, "SelectedUser.Name"); });

                StringAssert.Contains(ex.Message, "null was encountered");
            }
        }
    }
}
