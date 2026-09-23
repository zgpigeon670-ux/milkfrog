using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class HitStopClock
    {
        public float Remaining { get; private set; }
        public void Request(float seconds) => Remaining = Mathf.Max(Remaining, Mathf.Clamp(seconds, 0, .15f));
        public float Consume(float dt) { float frozen = Mathf.Min(dt, Remaining); Remaining -= frozen; return dt - frozen; }
        public void Reset() => Remaining = 0;
    }

    public readonly struct CombatContact
    {
        public readonly HitEvent Hit;
        public readonly Vector3 Position;
        public CombatContact(HitEvent hit, Vector3 position) { Hit = hit; Position = position; }
    }

    public sealed class CombatFeedback : MonoBehaviour
    {
        public CombatDemoSession session;
        public System.Func<CombatCore, CombatActor> ResolveActor;
        public CombatActor[] actors;
        public DemoCamera cameraRig;
        public Material flashMaterial;
        public bool hitStop = true, flashes = true, sound = true, cameraImpulse = true;
        [Range(0, 1)] public float volume = .35f;
        public float deflectFreeze = .07f;
        public float hitFreeze = .025f, blockFreeze = .012f, breakFreeze = .11f;
        public HitStopClock Clock { get; } = new HitStopClock();
        public int ActiveFlashes { get; private set; }
        readonly GameObject[] pool = new GameObject[8];
        readonly float[] lifetimes = new float[8];
        readonly float[] durations = new float[8];
        readonly float[] sizes = new float[8];
        readonly Renderer[][] impactRenderers = new Renderer[8][];
        CombatActorView playerView, enemyView;
        AudioClip hitClip, blockClip, deflectClip;
        AudioSource audioSource;
        MaterialPropertyBlock color;
        int next;
        void Awake()
        {
            color = new MaterialPropertyBlock();
            if (session != null)
            {
                playerView = session.player.GetComponent<CombatActorView>();
                enemyView = session.enemy.GetComponent<CombatActorView>();
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            hitClip = Tone("Hit", 145, .10f);
            blockClip = Tone("Block", 520, .09f);
            deflectClip = Tone("Deflect", 1100, .16f);
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pool[i].name = "Pooled impact " + i;
                pool[i].transform.SetParent(transform);
                var collider = pool[i].GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                pool[i].GetComponent<Renderer>().sharedMaterial = flashMaterial;
                for (int ray = 0; ray < 8; ray++)
                {
                    var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    spark.name = "Radial spark"; spark.transform.SetParent(pool[i].transform, false);
                    var sparkCollider = spark.GetComponent<Collider>(); sparkCollider.enabled = false; Destroy(sparkCollider);
                    spark.transform.localRotation = Quaternion.Euler(0, 0, ray * 45);
                    spark.transform.localPosition = spark.transform.up * 1.65f;
                    spark.transform.localScale = new Vector3(.065f, 1.4f, .065f);
                    spark.GetComponent<Renderer>().sharedMaterial = flashMaterial;
                }
                impactRenderers[i] = pool[i].GetComponentsInChildren<Renderer>();
                pool[i].SetActive(false);
            }
            if (session != null) { session.ContactResolved += OnContact; session.RoundReset += ResetFeedback; }
        }

        public void OnContact(CombatContact contact)
        {
            var result = contact.Hit.Result;
            if (result == HitResult.Ignore) return;
            bool parry = result == HitResult.Deflect;
            bool blocked = result == HitResult.Block;
            bool broken = contact.Hit.Defender.State == CombatState.PostureBroken;
            if (hitStop) Clock.Request(broken ? breakFreeze : parry ? deflectFreeze : blocked ? blockFreeze : hitFreeze);
            if (cameraImpulse) cameraRig.Impulse(broken ? .14f : parry ? .11f : blocked ? .02f : .045f);
            if (sound) audioSource.PlayOneShot(broken || parry ? deflectClip : blocked ? blockClip : hitClip, broken ? volume * 1.25f : volume);
            if (flashes)
            {
                int slot = next++ % pool.Length;
                pool[slot].transform.position = contact.Position;
                sizes[slot] = broken ? .22f : parry ? .16f : blocked ? .04f : .08f;
                pool[slot].transform.localScale = Vector3.one * sizes[slot];
                pool[slot].transform.rotation = cameraRig.transform.rotation;
                color.SetColor("_BaseColor", parry ? new Color(2, 1.4f, .45f) : result == HitResult.Block ? new Color(1,.72f,.35f) : new Color(1, .3f, .2f));
                foreach (var renderer in impactRenderers[slot]) renderer.SetPropertyBlock(color);
                durations[slot] = lifetimes[slot] = broken ? .28f : parry ? .20f : blocked ? .07f : .11f;
                pool[slot].SetActive(true);
                var flashedCore = parry ? contact.Hit.Attacker : contact.Hit.Defender;
                var view = ResolveActor != null ? ResolveActor(flashedCore)?.GetComponent<CombatActorView>() :
                    session != null && flashedCore == session.player.Core ? playerView : enemyView;
                if (result != HitResult.Block && view != null) view.Flash(broken ? new Color(1.8f,.85f,.35f) : parry ? new Color(1.6f,1.6f,1.6f) : new Color(1,.15f,.1f), broken ? .28f : parry ? .20f : .12f);
            }
        }

        public void TickReal(float dt)
        {
            ActiveFlashes = 0;
            if (playerView != null) playerView.TickFlash(dt);
            if (enemyView != null) enemyView.TickFlash(dt);
            if (actors != null) foreach (var actor in actors) if (actor != null) actor.GetComponent<CombatActorView>()?.TickFlash(dt);
            for (int i = 0; i < pool.Length; i++)
            {
                if (lifetimes[i] <= 0) continue;
                lifetimes[i] = Mathf.Max(0, lifetimes[i] - dt);
                float remaining = lifetimes[i] / durations[i];
                pool[i].transform.localScale = Vector3.one * sizes[i] * (1 + (1-remaining) * .8f) * Mathf.Min(1, remaining * 4);
                if (lifetimes[i] <= 0) pool[i].SetActive(false); else ActiveFlashes++;
            }
        }

        public void ResetFeedback()
        {
            Clock.Reset(); ActiveFlashes = next = 0;
            for (int i = 0; i < pool.Length; i++) { lifetimes[i] = 0; if (pool[i] != null) pool[i].SetActive(false); }
            if (audioSource != null) audioSource.Stop();
            if (cameraRig != null) cameraRig.ResetImpulse();
            if (actors != null) foreach (var actor in actors) if (actor != null) actor.GetComponent<CombatActorView>()?.ResetFlash();
            if (session != null)
            {
                if (session.player != null) session.player.GetComponent<CombatActorView>().ResetFlash();
                if (session.enemy != null) session.enemy.GetComponent<CombatActorView>().ResetFlash();
            }
        }

        static AudioClip Tone(string name, float hz, float duration)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, envelope = Mathf.Exp(-t * 35) * Mathf.Min(1, t * 1000);
                samples[i] = envelope * (.55f * Mathf.Sin(2 * Mathf.PI * hz * t) + .18f * Mathf.Sin(2 * Mathf.PI * hz * 2.71f * t));
            }
            var clip = AudioClip.Create(name + " (procedural)", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        void OnDisable() => ResetFeedback();
        void OnDestroy()
        {
            if (session != null) { session.ContactResolved -= OnContact; session.RoundReset -= ResetFeedback; }
            Destroy(hitClip); Destroy(blockClip); Destroy(deflectClip);
        }
    }
}

