using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    public NavMeshAgent Agent { get; private set; }
    public AIAnimationController aiAnimationController { get; private set; }
    public Transform[] Waypoints;
    public Transform Player;

    public float AttackRange = 2f;
    public LayerMask PlayerLayer;
    public StateType currentState;
    public StateMachine StateMachine { get; private set;}

    [Header("Attack Settings")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Vision Settings")]
    public float viewDistance = 10f;
    public float viewAngle = 90f;
    public float eyeHeight = 1.6f; 
    public LayerMask obstacleMask;
    public LayerMask playerMask;

    [Header("Vision Stability")]
    public float visionPersistence = 0.5f; 
    private float lastSeenTime = -999f;

    [Header("Patrol Settings")]
    public float patrolWaitTime = 1.5f;


    // Add State Machine code Here
    void Start() 
    { 
        //todo
        if (leftHandTransform == null || rightHandTransform == null)
        {
            Debug.LogWarning($"{name}: One or both hand transforms are not assigned.", this);
        }
        if (Player == null){
            GameObject playerObj = GameObject.FindWithTag("Player");
            if(playerObj != null){
            Player = playerObj.transform;
            }
            else{
                Debug.LogWarning($"{name}: No Player assigned and no object tagged 'Player' found.", this);
            }
        }
        
        Agent = GetComponent<NavMeshAgent>(); 
        aiAnimationController = GetComponent<AIAnimationController>(); 
 
        StateMachine = new StateMachine(); 
        StateMachine.AddState(new IdleState(this)); 
        StateMachine.AddState(new PatrolState(this)); 
        StateMachine.AddState(new ChaseState(this)); 
        StateMachine.AddState(new AttackState(this)); 
 
        StateMachine.TransitionToState(StateType.Idle); 
    } 
 
    void Update() 
    { 
        StateMachine.Update(); 
        currentState = StateMachine.GetCurrentStateType(); 
    } 

    
 
    public bool CanSeePlayer()
    {
        if (Player == null)
        {
            return false;
        }
        bool recentlySeen = Time.time - lastSeenTime < visionPersistence;

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = Player.position + Vector3.up * 0.5f;
        Vector3 directionToPlayer = (targetPosition - eyePosition).normalized;
        float distanceToPlayer = Vector3.Distance(eyePosition, targetPosition);
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer > viewAngle / 2f)
        {
            return recentlySeen;
        }

        if (distanceToPlayer > viewDistance)
        {
            return recentlySeen;
        }

        int visionMask = obstacleMask | playerMask;
        if (Physics.Raycast(eyePosition, directionToPlayer, out RaycastHit hit, distanceToPlayer, visionMask, QueryTriggerInteraction.Ignore))
        {
            bool hitIsPlayer = ((1 << hit.collider.gameObject.layer) & playerMask) != 0 || hit.transform.IsChildOf(Player);
            if (!hitIsPlayer)
            {
                return recentlySeen;
            }
        }

        lastSeenTime = Time.time;
        return true;
    }

    public bool CheckHandsCollision(out GameObject collidedObject, string Tag)
    {
        Transform[] handTransforms = { leftHandTransform, rightHandTransform };

        foreach (Transform hand in handTransforms)
        {

            if(hand == null)
            {
                continue;
            }
            Collider[] hits = Physics.OverlapSphere(hand.position, 0.5f, PlayerLayer);

            foreach (var hit in hits)
            {
                if (hit.CompareTag(Tag))
                {
                    collidedObject = hit.gameObject;
                    return true;
                }
            }
        }
        collidedObject = null;
        return false;
    }
    


    public bool IsPlayerInAttackRange()
    {
        if(Player == null)
            return false;

        float distanceToPlayer = Vector3.Distance(transform.position, Player.position);
        return distanceToPlayer <= AttackRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewDistance);
    }

}
