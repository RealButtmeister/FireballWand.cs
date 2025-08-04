using System.Collections.Generic;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("FireballWand", "YourName", "1.0.2")]
    [Description("Torch that casts a zero-damage fireball with a cooldown")]

    public class FireballWand : RustPlugin
    {
        private const string WandShortName = "torch";
        private const string WandDisplayName = "Fireball Wand";

        private Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();
        private const float CooldownSeconds = 30f;

        [ChatCommand("givefirewand")]
        private void GiveFireWand(BasePlayer player, string command, string[] args)
        {
            Item item = ItemManager.CreateByName(WandShortName, 1);

            if (item == null)
            {
                player.ChatMessage("❌ Couldn't create wand.");
                return;
            }

            item.name = WandDisplayName;
            item.text = "Casts a fireball (visual only, no damage).";
            item.MarkDirty();
            item.MoveToContainer(player.inventory.containerBelt);
            player.ChatMessage("🔥 Fireball Wand added to your belt.");
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !input.WasJustPressed(BUTTON.FIRE_PRIMARY)) return;

            var heldItem = player.GetActiveItem();
            if (heldItem == null || heldItem.info.shortname != WandShortName || heldItem.name != WandDisplayName) return;

            // Cooldown check
            if (cooldowns.TryGetValue(player.userID, out float nextTime) && Time.time < nextTime)
            {
                float remaining = Mathf.Ceil(nextTime - Time.time);
                player.ChatMessage($"⏳ Fireball is on cooldown ({remaining}s remaining).");
                return;
            }

            cooldowns[player.userID] = Time.time + CooldownSeconds;
            CastFireball(player);
        }

        private void CastFireball(BasePlayer player)
        {
            Vector3 pos = player.transform.position + player.eyes.BodyForward() * 1.5f + Vector3.up * 1.2f;
            Quaternion rot = Quaternion.LookRotation(player.eyes.BodyForward());

            BaseEntity rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_fire.prefab", pos, rot);
            if (rocket == null)
            {
                player.ChatMessage("⚠️ Failed to cast fireball.");
                return;
            }

            // Prevent rocket from dealing damage
            rocket.GetComponent<BaseEntity>()?.SetFlag(BaseEntity.Flags.Reserved8, true); // disarm if applicable
            rocket.SendMessage("InitializeVelocity", player.eyes.BodyForward() * 75f);
            rocket.Spawn();

            // Optional: visual effect at cast point
            Effect.server.Run("assets/bundled/prefabs/fx/item_use/fire/fireball_small.prefab", pos);

            // Remove damage on explosion by overriding damage hooks
            timer.Once(0.1f, () =>
            {
                if (rocket != null && !rocket.IsDestroyed)
                {
                    rocket.gameObject.AddComponent<NoDamageComponent>();
                }
            });
        }

        // Component to override damage behavior
        public class NoDamageComponent : MonoBehaviour
        {
            void OnTriggerEnter(Collider other)
            {
                // Prevent any default explosion damage — do nothing
            }

            void OnCollisionEnter(Collision collision)
            {
                // Do nothing
            }
        }
    }
}
