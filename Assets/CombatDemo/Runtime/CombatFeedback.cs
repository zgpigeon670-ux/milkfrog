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
        public DemoCamera cameraRig;
        public Material flashMaterial;
        public bool hitStop = true, flashes = true, sound = true, cameraImpulse = true;
        [Range(0, 1)] public float volume = .35f;
        public float deflectFreeze = .04f;
        public HitStopClock Clock { get; } = new HitStopClock();
        public int ActiveFlashes { get; private set; }
        readonly GameObject[] pool = new GameObject[8];
        readonly float[] lifetimes = new float[8];
        AudioClip hitClip, blockClip, deflectClip;
        AudioSource audioSource;
        MaterialPropertyBlock color;
        int next;
        void Awake()
        {
            color = new MaterialPropertyBlock();
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
                pool[i].SetActive(false);
            }
            session.ContactResolved += OnContact;
            session.RoundReset += ResetFeedback;
        }

        void OnContact(CombatContact contact)
        {
            var result = contact.Hit.Result;
            if (result == HitResult.Ignore) return;
            bool parry = result == HitResult.Deflect;
            if (parry && hitStop) Clock.Request(deflectFreeze);
            if (cameraImpulse) cameraRig.Impulse(parry ? .11f : .035f);
            if (sound) audioSource.PlayOneShot(parry ? deflectClip : result == HitResult.Block ? blockClip : hitClip, volume);
            if (flashes)
            {
                int slot = next++ % pool.Length;
                pool[slot].transform.position = contact.Position;
                pool[slot].transform.localScale = Vector3.one * (parry ? .3f : .16f);
                color.SetColor("_BaseColor", parry ? new Color(1, .86f, .25f) : result == HitResult.Block ? Color.cyan : new Color(1, .3f, .2f));
                pool[slot].GetComponent<Renderer>().SetPropertyBlock(color);
                lifetimes[slot] = parry ? .15f : .09f;
                pool[slot].SetActive(true);
                var victim = contact.Hit.Defender == session.player.Core ? session.player : session.enemy;
                if (victim.TryGetComponent<CombatActorView>(out var view)) view.Flash(parry ? Color.white : Color.red, .08f);
            }
        }

        public void TickReal(float dt)
        {
            ActiveFlashes = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (lifetimes[i] <= 0) continue;
                lifetimes[i] = Mathf.Max(0, lifetimes[i] - dt);
                if (lifetimes[i] <= 0) pool[i].SetActive(false); else ActiveFlashes++;
            }
        }

        public void ResetFeedback()
        {
            Clock.Reset(); ActiveFlashes = next = 0;
            for (int i = 0; i < pool.Length; i++) { lifetimes[i] = 0; if (pool[i] != null) pool[i].SetActive(false); }
            if (audioSource != null) audioSource.Stop();
            if (cameraRig != null) cameraRig.ResetImpulse();
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

