using UnityEngine; 
using UnityEngine.SceneManagement; 
 
public class AttackState : IState 
{ 
    private AIController aiController; 
    private float attackCooldown = 1.5f; // seconds between attacks 
    private float lastAttackTime = -999f; 
    private float turnSpeed =  360f; //degrees per second
 
    public StateType Type => StateType.Attack; 
 
    public AttackState(AIController aiController) 
    { 
        this.aiController = aiController; 
    } 
 
    public void Enter() 
    { 
        aiController.Agent.isStopped = true; 
    } 
 
    public void Execute() 
    { 
        //checks if player exists. will patrol if none found
        if(aiController.Player == null){
            aiController.StateMachine.TransitionToState(StateType.Patrol);
            return;
        }

        // If player moves out of attack range, go back to chase 
        if (!aiController.IsPlayerInAttackRange()) 
        { 
            aiController.StateMachine.TransitionToState(StateType.Chase); 
            return; 
        } 
 
        FacePlayer();

        // Limit how often the AI attacks 
        if (Time.time - lastAttackTime > attackCooldown) 
        { 
            lastAttackTime = Time.time; 
            if(aiController.aiAnimationController != null){
                aiController.aiAnimationController.animator.SetTrigger("doAttack"); 
            }
        } 
    } 
 
    private void FacePlayer()
    {
        Vector3 direction = aiController.Player.position - aiController.transform.position;
        direction.y = 0f;

        if(direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction); //rotation/angle at which the enemy is pointing straight to the player
        aiController.transform.rotation = Quaternion.RotateTowards(aiController.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            //RotateTowards(from, to, maxDegrees)
            //aiController.transform.rotation,  ==  where it's facing now
            //targetRotation,                   ==  where we want it to face
            //turnSpeed * Time.deltaTime);      ==  max degrees to turn this frame
    }
 
    public void Exit() 
    { 
        aiController.Agent.isStopped = false; 
    } 
} 