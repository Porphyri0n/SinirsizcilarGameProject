using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

// Craft kuyruğu — aynı anda tek craft, biten craft sonrası sıradaki başlar.
// Enqueue ile tarif eklenir, Coroutine süreyi sayar.
// EventBus.FireCraftStarted başta, EventBus.FireCraftCompleted bitişte tetiklenir.
public class CraftQueueManager : NetworkBehaviour
{
    private readonly NetworkVariable<float> netProgress = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString32Bytes> netRecipeName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly Queue<RecipeData> queue = new Queue<RecipeData>();
    private Coroutine running;

    public bool IsCrafting => running != null;
    public RecipeData CurrentRecipe { get; private set; }
    public int QueueCount => queue.Count;

    // UI için ek bilgi — anlık ilerleme (0..1)
    public float Progress { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && !string.IsNullOrEmpty(netRecipeName.Value.ToString()))
        {
            RecipeData recipe = FindRecipe(netRecipeName.Value.ToString());
            if (recipe != null)
            {
                SyncState(recipe, netProgress.Value);
            }
        }
    }

    private RecipeData FindRecipe(string recipeName)
    {
        var catalog = FindObjectOfType<RecipeCatalog>();
        if (catalog == null) return null;
        foreach (var r in catalog.All)
        {
            if (r.recipeName == recipeName) return r;
        }
        return null;
    }

    // Kuyruğa craft ekle. CraftingUI tıklayınca buraya çağrı gelir.
    public void Enqueue(RecipeData recipe)
    {
        if (recipe == null) return;
        queue.Enqueue(recipe);
        TryStartNext();
    }

    public void CancelAll()
    {
        queue.Clear();
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }
        CurrentRecipe = null;
        Progress = 0f;
        if (IsServer)
        {
            netRecipeName.Value = "";
            netProgress.Value = 0f;
        }
    }

    // İptal sırasında kaynak iadesi için bekleyen tüm tarifler: aktif olan + kuyruktakiler.
    public IEnumerable<RecipeData> GetPendingRecipes()
    {
        if (CurrentRecipe != null) yield return CurrentRecipe;
        foreach (RecipeData recipe in queue) yield return recipe;
    }

    private void TryStartNext()
    {
        if (running != null) return;       // bir craft zaten çalışıyor
        if (queue.Count == 0) return;

        RecipeData next = queue.Dequeue();
        if (next == null) { TryStartNext(); return; }

        running = StartCoroutine(CraftRoutine(next));
    }

    public void SyncState(RecipeData recipe, float progress)
    {
        if (recipe == null) return;
        
        // Eğer zaten bir şey çalışıyorsa durdur
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        CurrentRecipe = recipe;
        Progress = progress;
        
        // Başlatma event'ini tekrar fire et ki UI güncellensin (duration'ı kalan süreye göre ayarla)
        float remainingDuration = recipe.craftDuration * (1f - progress);
        EventBus.FireCraftStarted(recipe, remainingDuration);

        // Kalan süreyi saymak için yeni bir routine başlat
        running = StartCoroutine(CraftRoutine(recipe, progress));
    }

    private IEnumerator CraftRoutine(RecipeData recipe, float startProgress = 0f)
    {
        CurrentRecipe = recipe;
        if (IsServer) netRecipeName.Value = recipe.recipeName;

        float duration = Mathf.Max(0.1f, recipe.craftDuration);

        // Eğer başlangıç progress'i varsa event'i zaten SyncState'de fire ettik.
        // Yoksa (normal akış) burada fire et.
        if (startProgress <= 0f)
            EventBus.FireCraftStarted(recipe, duration);

        float t = startProgress * duration;
        while (t < duration)
        {
            t += Time.deltaTime;
            Progress = Mathf.Clamp01(t / duration);
            if (IsServer) netProgress.Value = Progress;
            yield return null;
        }

        EventBus.FireCraftCompleted(recipe);

        CurrentRecipe = null;
        Progress = 0f;
        if (IsServer)
        {
            netRecipeName.Value = "";
            netProgress.Value = 0f;
        }
        running = null;

        TryStartNext();     // sıradakine geç
    }
}
