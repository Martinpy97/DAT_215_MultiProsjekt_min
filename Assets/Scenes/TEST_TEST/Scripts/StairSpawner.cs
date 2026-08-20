using System.Collections;
using UnityEngine;

public class StairSpawner : MonoBehaviour
{
    [Header("Trapp")]
    [Tooltip("Prefab-en som skal spawnes.")]
    [SerializeField] private GameObject staircasePrefab;

    [Tooltip("Posisjonen og rotasjonen der trappen skal spawnes.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Legger den nye trappen under dette objektet i Hierarchy.")]
    [SerializeField] private bool spawnAsChild;

    [Header("Byggeanimasjon")]
    [Tooltip("Viser og løfter trinnene ett etter ett.")]
    [SerializeField] private bool buildStepByStep = true;

    [SerializeField, Min(0f)] private float delayBetweenSteps = 0.12f;
    [SerializeField, Min(0.01f)] private float stepMoveDuration = 0.2f;
    [SerializeField, Min(0f)] private float riseDistance = 0.5f;

    private GameObject spawnedStaircase;

    public void SpawnStairs()
    {
        if (spawnedStaircase != null)
        {
            return;
        }

        if (staircasePrefab == null)
        {
            Debug.LogError("Ingen trappe-prefab er koblet til StairSpawner.", this);
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        Transform parent = spawnAsChild ? transform : null;

        spawnedStaircase = Instantiate(
            staircasePrefab,
            point.position,
            point.rotation,
            parent
        );

        if (buildStepByStep)
        {
            StartCoroutine(BuildStairs());
        }
    }

    private IEnumerator BuildStairs()
    {
        int stepCount = spawnedStaircase.transform.childCount;
        Transform[] steps = new Transform[stepCount];
        Vector3[] finishedPositions = new Vector3[stepCount];

        for (int i = 0; i < stepCount; i++)
        {
            steps[i] = spawnedStaircase.transform.GetChild(i);
            finishedPositions[i] = steps[i].localPosition;
            steps[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < stepCount; i++)
        {
            Transform step = steps[i];
            Vector3 finishedPosition = finishedPositions[i];
            Vector3 startPosition = finishedPosition + Vector3.down * riseDistance;

            step.localPosition = startPosition;
            step.gameObject.SetActive(true);

            float elapsedTime = 0f;

            while (elapsedTime < stepMoveDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / stepMoveDuration);
                progress = Mathf.SmoothStep(0f, 1f, progress);

                step.localPosition = Vector3.Lerp(
                    startPosition,
                    finishedPosition,
                    progress
                );

                yield return null;
            }

            step.localPosition = finishedPosition;

            if (delayBetweenSteps > 0f)
            {
                yield return new WaitForSeconds(delayBetweenSteps);
            }
        }
    }
}
