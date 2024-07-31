using UnityEngine;
using UnityEngine.SceneManagement;
using Crest;
using System.Collections;

public class FinWhaleAI : MonoBehaviour
{
    private Animator animator;
    private Vector3 targetDirection;
    private float timeUntilNextDirectionChange;
    private float timeUntilNextAnimation;
    private const float minSpeed = 3f;
    private const float maxSpeed = 7f;
    private float currentSpeed;
    private const float rotationSpeed = 0.5f;
    private const float raycastOriginOffset = 13f;
    private const float raycastDistance = 20f;
    private const float minScale = 0.8f;
    private const float maxScale = 1.4f;

    private Vector3[] queryPoints = new Vector3[1];
    private Vector3[] resultDisps = new Vector3[1];

    private Vector3 moveDirection;
    private float directionChangeInterval = 10f;
    private float directionLerpSpeed = 0.6f;
    private float bendAmount;
    private float turnRate;

    private Transform playerTransform;
    private const float despawnDistance = 3000f;

    void Start()
    {
        animator = GetComponent<Animator>();
        SetRandomDirection();
        SetRandomAnimationTime();
        SetInitialOrientation();
        FindPlayer();
        currentSpeed = Random.Range(minSpeed, maxSpeed);
        SetRandomScale();
    }

    void Update()
    {
        DetectAndAvoidObstacles();
        UpdateMovement();
        AdjustHeightToWave();
        HandleAnimationTrigger();
        CheckDistanceToPlayer();

        // Occasionally change direction
        timeUntilNextDirectionChange -= Time.deltaTime;
        if (timeUntilNextDirectionChange <= 0f)
        {
            SetRandomDirection();
            timeUntilNextDirectionChange = directionChangeInterval;
        }

        // Smooth the bend amount based on turn rate
        float bendScalingFactor = 0.5f;
        bendAmount = Mathf.Lerp(bendAmount, turnRate * bendScalingFactor, Time.deltaTime * 0.5f);
        bendAmount = Mathf.Clamp(bendAmount, -1f, 1f);
        animator.SetFloat("BendAmount", bendAmount);
    }

    private void DetectAndAvoidObstacles()
    {
        RaycastHit hit;
        Vector3 rayDirection = transform.forward;
        Vector3 rayOrigin = transform.position + rayDirection * raycastOriginOffset;

        if (Physics.SphereCast(rayOrigin, 5f, rayDirection, out hit, raycastDistance))
        {
            if (hit.collider != null)
            {
                Vector3 hitNormal = hit.normal;
                Vector3 newDirection = Vector3.Reflect(rayDirection, hitNormal);
                targetDirection = newDirection;
                StartCoroutine(AvoidObstacle());
            }
        }
    }

    private IEnumerator AvoidObstacle()
    {
        while (Physics.SphereCast(transform.position + transform.forward * raycastOriginOffset, 5f, transform.forward, out RaycastHit hit, raycastDistance))
        {
            Vector3 hitNormal = hit.normal;
            Vector3 newDirection = Vector3.Reflect(transform.forward, hitNormal);
            targetDirection = newDirection;
            yield return null;
        }

        SetRandomDirection();
    }

    private void UpdateMovement()
    {
        Vector3 previousDirection = moveDirection;
        moveDirection = Vector3.Lerp(moveDirection, targetDirection, directionLerpSpeed * Time.deltaTime).normalized;

        transform.position += transform.forward * Time.deltaTime * currentSpeed;

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

            turnRate = Vector3.SignedAngle(previousDirection, moveDirection, Vector3.up);
        }
        else
        {
            turnRate = 0f;
        }
    }

    private void AdjustHeightToWave()
    {
        if (OceanRenderer.Instance == null) return;

        queryPoints[0] = transform.position;
        int queryStatus = OceanRenderer.Instance.CollisionProvider.Query(GetHashCode(), 1.0f, queryPoints, resultDisps, null, null);
        if (OceanRenderer.Instance.CollisionProvider.RetrieveSucceeded(queryStatus))
        {
            float height = OceanRenderer.Instance.SeaLevel + resultDisps[0].y - 2.0f;
            Vector3 targetPosition = new Vector3(transform.position.x, height, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 2.5f);
        }
    }

    private void HandleAnimationTrigger()
    {
        timeUntilNextAnimation -= Time.deltaTime;
        if (timeUntilNextAnimation <= 0f)
        {
            TriggerRandomAnimation();
            SetRandomAnimationTime();
        }
    }

    private void TriggerRandomAnimation()
    {
        int randomAnimation = Random.Range(0, 2);
        animator.SetTrigger(randomAnimation == 0 ? "Surface" : "Breach");
    }

    private void SetRandomAnimationTime()
    {
        timeUntilNextAnimation = Random.Range(30, 90);
    }

    private void SetRandomDirection()
    {
        float randomAngle = Random.Range(0, 360);
        targetDirection = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward;
    }

    private void SetInitialOrientation()
    {
        Vector3 forwardDirection = new Vector3(targetDirection.x - transform.position.x, 0, targetDirection.z - transform.position.z);
        transform.rotation = Quaternion.LookRotation(forwardDirection);
    }

    private void SetRandomScale()
    {
        float randomScale = Random.Range(minScale, maxScale);
        transform.localScale = new Vector3(randomScale, randomScale, randomScale);
    }

    private void FindPlayer()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("PlayerController"))
            {
                playerTransform = obj.transform;
                return;
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            Transform playerTransform = rootObject.transform.Find("OVRPlayerController (controller)");
            if (playerTransform != null)
            {
                this.playerTransform = playerTransform;
                return;
            }
        }
    }

    private void CheckDistanceToPlayer()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > despawnDistance)
        {
            Destroy(gameObject);
        }
    }
}
