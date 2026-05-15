using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Craft kuyruğu — aynı anda tek craft, biten craft sonrası sıradaki başlar.
// Enqueue ile tarif eklenir, Coroutine süreyi sayar.
// EventBus.FireCraftStarted başta, EventBus.FireCraftCompleted bitişte tetiklenir.
public class CraftQueueManager : MonoBehaviour
{
    private readonly Queue<RecipeData> queue = new Queue<RecipeData>();
    private Coroutine running;

    public bool IsCrafting => running != null;
    public RecipeData CurrentRecipe { get; private set; }
    public int QueueCount => queue.Count;

    // UI için ek bilgi — anlık ilerleme (0..1)
    public float Progress { get; private set; }

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
    }

    private void TryStartNext()
    {
        if (running != null) return;       // bir craft zaten çalışıyor
        if (queue.Count == 0) return;

        RecipeData next = queue.Dequeue();
        if (next == null) { TryStartNext(); return; }

        running = StartCoroutine(CraftRoutine(next));
    }

    private IEnumerator CraftRoutine(RecipeData recipe)
    {
        CurrentRecipe = recipe;
        float duration = Mathf.Max(0.1f, recipe.craftDuration);

        EventBus.FireCraftStarted(recipe, duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Progress = Mathf.Clamp01(t / duration);
            yield return null;
        }

        EventBus.FireCraftCompleted(recipe);

        CurrentRecipe = null;
        Progress = 0f;
        running = null;

        TryStartNext();     // sıradakine geç
    }
}
