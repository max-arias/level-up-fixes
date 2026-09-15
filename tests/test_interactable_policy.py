"""Guards the interactable item-source policy against omissions.

The policy itself lives in `src/LevelUpChoicesFixes.cs` (class `ItemSources`). This test parses
those string lists and checks them against `tests/data/interactable_inventory.json`, which was
dumped from the shipping game's stage-pool assets and from Item Qualities' content.

The failure this prevents: `isctemporaryitemsshop` spawns from 23 stage pools but its name does
not match the `iscchest*` shape, so a hand-written chest list missed it and the DLC3 Temporary
Item Distributor kept spawning on stages where item sources are supposed to be removed. Any
interactable that is neither blocked nor explicitly kept now fails the suite.
"""

import json
import pathlib
import re
import unittest

REPO = pathlib.Path(__file__).resolve().parent.parent
SOURCE = REPO / "src" / "LevelUpChoicesFixes.cs"
INVENTORY = REPO / "tests" / "data" / "interactable_inventory.json"


# Interactables that intentionally stay spawnable. Every entry needs a reason: an unclassified
# new interactable must fail this suite so a human decides, rather than silently spawning.
KEPT = {
    # Destructible scenery with no reward.
    "iscbarrel1": "plain barrel, drops nothing",
    "iscradartower": "radio scanner, reveals the map",
    # Paid drone allies, not items.
    "iscbrokendrone1": "drone ally for gold",
    "iscbrokendrone2": "drone ally for gold",
    "iscbrokenemergencydrone": "drone ally for gold",
    "iscbrokenequipmentdrone": "drone ally for gold",
    "iscbrokenflamedrone": "drone ally for gold",
    "iscbrokenmegadrone": "drone ally for gold",
    "iscbrokenmissiledrone": "drone ally for gold",
    "iscbrokenturret1": "turret ally for gold",
    "iscbrokenbombardmentdrone": "drone ally for gold",
    "iscbrokencleanupdrone": "drone ally for gold",
    "iscbrokencopycatdrone": "drone ally for gold",
    "iscbrokenhaulerdrone": "drone ally for gold",
    "iscbrokenjailerdrone": "drone ally for gold",
    "iscbrokenjunkdrone": "drone ally for gold",
    "iscbrokenrechargedrone": "drone ally for gold",
    "isctripledroneshop": "drone vendor, sells drones",
    # Drone services that consume items or drones without granting items.
    "iscdroneassemblystation": "consumes an item to upgrade drones",
    "iscdronecombinerstation": "combines drones",
    # Equipment barrel is permitted through a four-per-stage spawn cap, not as an unbounded pool card.
    "iscequipmentbarrel": "capped at four per stage",
    # Requested shrine rewards/services remain part of the stage pool.
    "iscshrineboss": "Shrine of the Mountain",
    "iscshrinebosssandy": "Shrine of the Mountain",
    "iscshrinebosssnowy": "Shrine of the Mountain",
    "iscshrinehalcyonite": "Halcyon Shrine",
    "iscshrinehalcyonitetier1": "Halcyon Shrine",
    "iscshrinegoldshoresaccess": "Altar of Gold",
    "iscshrinecolossusaccess": "Shrine of Shaping",
    "iscshrinehealing": "Shrine of the Woods",
    "iscshrinecombat": "Collective Shrine of Combat",
    "iscshrinecombatsandy": "Collective Shrine of Combat",
    "iscshrinecombatsnowy": "Collective Shrine of Combat",
    # Void content, kept on request even though the cradles grant void items.
    "iscvoidcamp": "void seed, kept on request",
    "iscvoidchest": "void cradle, kept on request",
    "iscvoidchestsacrificeon": "void cradle, kept on request",
    "iscvoidcoinbarrel": "void stalk, kept on request",
    "iscvoidtriple": "void potential, kept on request",
}

# These remain removed by LevelUpChoices' own stage-pool filter. They must stay explicit rather than
# using an iscshrine prefix because the shrine reward/services listed in KEPT are intentionally kept.
PARENT_BLOCKED_SHRINES = {
    "iscshrineblood", "iscshrinebloodsandy", "iscshrinebloodsnowy",
    "iscshrinechance", "iscshrinechancesandy", "iscshrinechancesnowy",
    "iscshrinecleanse", "iscshrinecleansesandy", "iscshrinecleansesnowy",
    "iscshrinerestack", "iscshrinerestacksandy", "iscshrinerestacksnowy",
}

# Item-granting interactables that are deliberately outside this policy because they are not
# stage item sources: they are enemy drops, mission rewards, or summons owned by the player.
OUT_OF_SCOPE_ITEM_GRANTS = {
    "isccommandchest": "vault/artifact reward chest, not a stage spawn",
    "iscscavbackpack": "Scavenger's Sack, an enemy drop",
    "iscsquidturret": "summoned by the Squid Polyp item",
}


def parse_policy():
    """Returns (blocked, quality_only) name sets parsed straight out of the C# source."""
    source = SOURCE.read_text()
    body = source.split("internal static class ItemSources", 1)[1].split("\n}", 1)[0]
    body = "\n".join(re.sub(r"//.*", "", line) for line in body.splitlines())
    quality_body = body.split("QualityItemSources", 1)[1].split("};", 1)[0]
    quality = {name.lower() for name in re.findall(r'"([^"]+)"', quality_body)}
    blocked = {name.lower() for name in re.findall(r'"([^"]+)"', body)}
    return blocked, quality


BLOCKED, QUALITY_ONLY = parse_policy()
INVENTORY_DATA = json.loads(INVENTORY.read_text())
POOL_CARDS = INVENTORY_DATA["pool_cards"]
QUALITY_CARDS = INVENTORY_DATA["item_qualities_cards"]


def is_blocked(name, quality_enabled=True):
    """Mirror of ItemSources.IsBlocked, including the Item Qualities option gate."""
    name = name.lower()
    if name in PARENT_BLOCKED_SHRINES:
        return True
    if name in QUALITY_ONLY:
        return quality_enabled
    return name in BLOCKED


class InteractablePolicy(unittest.TestCase):
    def test_inventory_is_not_truncated(self):
        # Without this, a corrupted or empty fixture would make every other test vacuously pass.
        self.assertGreaterEqual(len(POOL_CARDS), 60)
        self.assertGreaterEqual(len(QUALITY_CARDS), 8)

    def test_every_stage_pool_interactable_is_classified(self):
        cards = sorted(set(POOL_CARDS) | set(QUALITY_CARDS))
        unclassified = [c for c in cards if not is_blocked(c) and c not in KEPT]
        self.assertEqual(unclassified, [], "classify these interactables as blocked or kept")

    def test_temporary_item_distributor_is_blocked(self):
        # The reported regression: 23 stage pools, DLC3, "Dispense 5 temporary items".
        self.assertIn("isctemporaryitemsshop", POOL_CARDS)
        self.assertTrue(is_blocked("isctemporaryitemsshop"))

    def test_requested_shrines_are_kept(self):
        requested = (
            "iscshrineboss", "iscshrinebosssandy", "iscshrinebosssnowy",
            "iscshrinehalcyonite", "iscshrinehalcyonitetier1",
            "iscshrinegoldshoresaccess", "iscshrinecolossusaccess", "iscshrinehealing",
            "iscshrinecombat", "iscshrinecombatsandy", "iscshrinecombatsnowy",
        )
        for name in requested:
            self.assertIn(name, POOL_CARDS)
            self.assertFalse(is_blocked(name), name)
        source = SOURCE.read_text()
        self.assertIn("PreserveParentCombatShrines", source)

    def test_quality_item_sources_follow_the_option(self):
        for name in ("iscqualitychest1", "iscqualitychest2", "iscqualityduplicator",
                     "iscqualityduplicatormilitary", "iscspeedonpickupbarrel",
                     "iscChest2Stealthed", "iscqualityscrapper", "iscqualityequipmentbarrel"):
            self.assertTrue(is_blocked(name), name)
            self.assertFalse(is_blocked(name, quality_enabled=False), name)

    def test_equipment_barrels_are_capped_and_equipment_shops_are_blocked(self):
        source = SOURCE.read_text()
        self.assertFalse(is_blocked("iscequipmentbarrel"))
        self.assertIn("EquipmentBarrelLimit = 4", source)
        self.assertIn("EquipmentBarrelLimitPrefix", source)
        self.assertIn("TrackEquipmentBarrelPostfix", source)
        self.assertTrue(is_blocked("isctripleshopequipment"))

    def test_void_sources_are_kept(self):
        for name in ("iscvoidchest", "iscvoidtriple", "iscvoidcoinbarrel", "iscvoidcamp"):
            self.assertFalse(is_blocked(name), name)

    def test_kept_and_blocked_do_not_overlap(self):
        overlaps = sorted(n for n in KEPT if is_blocked(n))
        self.assertEqual(overlaps, [])

    def test_kept_entries_all_appear_in_the_inventory(self):
        stale = sorted(n for n in KEPT if n not in POOL_CARDS and n not in QUALITY_CARDS)
        self.assertEqual(stale, [], "kept entries that no longer exist in the game")

    def test_out_of_scope_grants_are_not_blocked(self):
        for name, reason in OUT_OF_SCOPE_ITEM_GRANTS.items():
            self.assertTrue(reason)
            self.assertFalse(is_blocked(name), name)

    def test_policy_names_have_no_duplicates(self):
        source = SOURCE.read_text()
        body = source.split("internal static class ItemSources", 1)[1].split("\n}", 1)[0]
        body = "\n".join(re.sub(r"//.*", "", line) for line in body.splitlines())
        names = [m.lower() for m in re.findall(r'"([^"]+)"', body)]
        duplicated = sorted({n for n in names if names.count(n) > 1})
        self.assertEqual(duplicated, [])


if __name__ == "__main__":
    unittest.main()
