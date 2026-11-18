using UnityEngine; 
using System.Collections; 
 
public class PatrolState : IState 
{ 
    private AIController aiController; 
    private int currentWaypointIndex = 0; 
    private bool isWaiting = false; 
    private Coroutine waitRoutine;
 
    public StateType Type => StateType.Patrol; 
 
    public PatrolState(AIController aiController) 
    { 
        this.aiController = aiController; 
    } 
 
    public void Enter() 
    { 
        aiController.Agent.isStopped = false; 
        MoveToNextWaypoint(); 
    } 

     public void Execute() 
    { 
        if (aiController.CanSeePlayer()) 
        { 
            aiController.StateMachine.TransitionToState(StateType.Chase); 
            return; 
        } 
 
        if (!isWaiting && !aiController.Agent.pathPending && aiController.Agent.remainingDistance <= aiController.Agent.stoppingDistance) 
        { 
            waitRoutine = aiController.StartCoroutine(WaitAndAnimate()); 
        } 
 
    } 
 
    public void Exit() 
    { 
        if(waitRoutine != null){
            aiController.StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        isWaiting = false;

        //rawr
        if(aiController.aiAnimationController != null){
            aiController.aiAnimationController.animator.ResetTrigger("doScream");
        }

        aiController.Agent.isStopped = false; 
    } 
 
    private IEnumerator WaitAndAnimate() 
    { 
        isWaiting = true; 
        aiController.Agent.isStopped = true; 
 
        // Play patrol point animation 
        if(aiController.aiAnimationController != null){
            aiController.aiAnimationController.animator.SetTrigger("doScream"); 
        }
        // Wait for animation duration
        yield return new WaitForSeconds(aiController.patrolWaitTime); 
 
        aiController.Agent.isStopped = false; 
        MoveToNextWaypoint(); 
        isWaiting = false; 
        waitRoutine = null;
    } 
 
    private void MoveToNextWaypoint() 
    { 
        if (aiController.Waypoints.Length == 0) 
            return; 
 
        aiController.Agent.destination = aiController.Waypoints[currentWaypointIndex].position; 
        currentWaypointIndex = (currentWaypointIndex + 1) % aiController.Waypoints.Length; 
    } 
} 