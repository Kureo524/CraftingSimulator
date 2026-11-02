using System.Linq;
using Nucleaire.Data;
using Nucleaire.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace Nucleaire.Tests
{
    /// <summary>Le câble universel pose un câble par type transporté.</summary>
    public class CableTests : SimTestBase
    {
        [Test]
        public void UniversalCable_PlacesEachKind()
        {
            var item = R("item_universal_cable");
            var variants = Db.TransmittersForItem(item);
            Assert.AreEqual(4, variants.Count);
            CollectionAssert.AreEquivalent(new[] { ResourceKind.Energy, ResourceKind.Fluid, ResourceKind.Gas, ResourceKind.Item }, variants.Select(v => v.Carries));
            int before = Session.Inventory.Count(item);
            int x = 1;
            foreach (var v in variants)
            {
                Assert.IsTrue(Session.Actions.TryPlace(item, new Vector3Int(x, 1, 0), out var err, v), err);
                Assert.AreEqual(v.Carries, ((TransmitterSegment)World.At(new Vector3Int(x, 1, 0))).TransmitterDef.Carries);
                x += 2;
            }
            Assert.AreEqual(before - 4, Session.Inventory.Count(item));
        }
    }
}
