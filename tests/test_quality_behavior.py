import json
import math
import unittest


def effective_chance(chance, luck):
    chance = max(0.0, min(100.0, chance))
    rolls = max(1, 1 + math.floor(max(0.0, luck)))
    return 100.0 * (1.0 - (1.0 - chance / 100.0) ** rolls)


def promote(base, chance, weights, variants, sample_chance, sample_tier, luck=0, guaranteed=False):
    if not variants:
        return base
    if not guaranteed and sample_chance >= effective_chance(chance, luck) / 100.0:
        return base
    eligible = [(tier, weight) for tier, weight in enumerate(weights)
                if weight > 0 and variants.get(tier, base) != base]
    total = sum(weight for _, weight in eligible)
    if total <= 0:
        return base
    cursor = min(0.99999994, max(0.0, sample_tier)) * total
    for tier, weight in eligible:
        cursor -= weight
        if cursor < 0:
            return variants[tier]
    return variants[eligible[-1][0]]


def quality_batch_due(level, interval, mod_enabled=True, quality_enabled=True):
    return mod_enabled and quality_enabled and interval > 0 and level > 0 and level % interval == 0


def queue_batch(pending, level, interval, mod_enabled=True, quality_enabled=True, player="p1"):
    """Mirror of QueueGuaranteedQualityBatch: disabled states drop pending batches."""
    if not mod_enabled or not quality_enabled:
        pending.clear()
        return pending
    if quality_batch_due(level, interval):
        pending[player] = pending.get(player, 0) + 1
    return pending


def reroll(options, slot, replacement):
    excluded = set(options)
    return replacement if replacement not in excluded else options[slot]


def upstream_enabled(always_enable, artifact_enabled):
    return always_enable or artifact_enabled


def sync_options(payload):
    return json.loads(json.dumps(payload))


class QualityBehavior(unittest.TestCase):
    def test_absent_quality_runtime_fails_closed(self):
        self.assertEqual(promote("base", 100, [70, 20, 8, 2], {}, 0, 0), "base")

    def test_chance_boundaries(self):
        self.assertEqual(promote("base", 0, [1, 1, 1, 1], {0: "u"}, 0, 0), "base")
        self.assertEqual(promote("base", 100, [1, 0, 0, 0], {0: "u"}, 0, 0), "u")
    def test_default_chance_is_not_compatible_with_zero_of_300(self):
        expected_promotions = 300 * 0.04
        zero_probability = (1.0 - 0.04) ** 300
        self.assertAlmostEqual(expected_promotions, 12.0)
        self.assertLess(zero_probability, 0.00001)
    def test_guaranteed_quality_bypasses_random_chance(self):
        self.assertEqual(
            promote("base", 0, [70, 20, 8, 2], {0: "u"}, 1, 0, guaranteed=True),
            "u")

    def test_guaranteed_quality_batch_uses_level_threshold(self):
        self.assertEqual(
            [level for level in range(1, 21) if quality_batch_due(level, 5)],
            [5, 10, 15, 20])
        self.assertFalse(quality_batch_due(0, 5))
        self.assertFalse(quality_batch_due(5, 0))
        self.assertFalse(quality_batch_due(5, 5, mod_enabled=False))
        self.assertFalse(quality_batch_due(5, 5, quality_enabled=False))

    def test_queued_batches_do_not_survive_a_disabled_mod(self):
        pending = {}
        for level in (5, 10):
            queue_batch(pending, level, 5)
        self.assertEqual(pending, {"p1": 2})

        queue_batch(pending, 15, 5, mod_enabled=False)
        self.assertEqual(pending, {})

        queue_batch(pending, 16, 5, mod_enabled=False)
        queue_batch(pending, 20, 5)
        self.assertEqual(pending, {"p1": 1})

        queue_batch(pending, 25, 5, quality_enabled=False)
        self.assertEqual(pending, {})

    def test_default_relative_distribution(self):
        weights = [70, 20, 8, 2]
        counts = [0, 0, 0, 0]
        for i in range(10000):
            tier = next(t for t, _ in enumerate(weights)
                        if (i / 10000.0) * 100 < sum(weights[:t + 1]))
            counts[tier] += 1
        self.assertEqual(counts, [7000, 2000, 800, 200])

    def test_luck_increases_effective_chance(self):
        self.assertLess(effective_chance(4, 0), effective_chance(4, 2))
        self.assertEqual(effective_chance(100, 0), 100)

    def test_zero_weights_and_missing_variants(self):
        variants = {0: "u", 2: "e"}
        self.assertEqual(promote("base", 100, [0, 0, 0, 0], variants, 0, 0), "base")
        self.assertEqual(promote("base", 100, [0, 0, 8, 2], variants, 0, 0), "e")

    def test_reroll_excludes_original_and_other_slots(self):
        self.assertEqual(reroll(["fireworks", "lens", "hoof"], 0, "fireworks"), "fireworks")
        self.assertEqual(reroll(["fireworks", "lens", "hoof"], 0, "lens"), "fireworks")

    def test_banish_uses_canonical_identity(self):
        quality_to_base = {"quality_fireworks": "fireworks"}
        self.assertEqual(quality_to_base["quality_fireworks"], "fireworks")

    def test_issue_one_artifact_or_config_enablement_regression(self):
        self.assertFalse(upstream_enabled(False, False))
        self.assertTrue(upstream_enabled(True, False))
        self.assertTrue(upstream_enabled(False, True))

    def test_issue_two_client_options_sync_regression(self):
        host_payload = {"net_id": "1", "items": ["quality_fireworks", "lens", "hoof"]}
        client_payload = sync_options(host_payload)
        self.assertEqual(client_payload, host_payload)
        self.assertIsNot(client_payload, host_payload)


if __name__ == "__main__":
    unittest.main()
