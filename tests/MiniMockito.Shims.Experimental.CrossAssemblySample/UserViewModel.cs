using System.Collections.ObjectModel;
using ExternalLib;

namespace CrossAssemblySample
{
    /// <summary>
    /// A generic sample item type defined in the rewrite-target assembly.  When the target assembly is
    /// rewritten and loaded into an isolated context, this type's identity differs from the test's
    /// reference — so it is inspected via the Phase 24 inspection API rather than cast.
    /// </summary>
    public class UserItem
    {
        public UserItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>
    /// A generic sample view-model that builds an <see cref="ObservableCollection{T}"/> of rewritten
    /// items from an external (faked) data source.  Demonstrates inspecting an object graph
    /// (collection + nested item properties) without strongly typed casting.
    /// </summary>
    public class UserViewModel
    {
        public ObservableCollection<UserItem> Items { get; } = new ObservableCollection<UserItem>();

        public UserItem? SelectedUser { get; private set; }

        // Loads a single item from the (faked) external db context.
        public void Load()
        {
            using (var db = new ExternalDbContext())
            {
                Items.Clear();
                var item = new UserItem(db.GetName(1));
                Items.Add(item);
                SelectedUser = item;
            }
        }

        // Loads multiple items to exercise collection inspection with more than one element.
        public void LoadMany()
        {
            using (var db = new ExternalDbContext())
            {
                Items.Clear();
                Items.Add(new UserItem(db.GetName(1)));
                Items.Add(new UserItem(db.GetName(2)));
                SelectedUser = Items[0];
            }
        }
    }
}
