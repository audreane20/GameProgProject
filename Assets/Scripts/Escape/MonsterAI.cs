using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// The hospital monster. "Red light, green light":
//  - It roams the halls on the NavMesh (so it walks around walls and furniture).
//  - When it spots the kid it stops and stares: eyes blaze red and the chase music starts.
//  - Stay still and it loses interest. Move while it's watching and it charges.
//  - If the kid freezes during a chase, it stops and stares again, right where it is.
//  - Catching the kid triggers the jumpscare and a level restart (EscapeGameManager).
// Crouching and turning off the flashlight make the kid harder to see. Sprinting is loud.
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    public enum State { Roam, Investigate, Watch, Chase, Search, Attack }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer eyeRenderer;
    [SerializeField] private Light eyeLight;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private Transform[] waypoints;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float investigateSpeed = 2.2f;
    [SerializeField] private float runSpeed = 4.6f;
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private Vector2 pauseAtWaypoint = new Vector2(1f, 3.5f);

    [Header("Sight")]
    [SerializeField] private float viewAngle = 120f;
    [SerializeField] private float viewDistance = 12f;
    [Tooltip("Extra sight distance when the kid's flashlight is on.")]
    [SerializeField] private float flashlightBonus = 7f;
    [Tooltip("Sight distance is multiplied by this when the kid is crouching.")]
    [SerializeField] private float crouchMultiplier = 0.6f;
    [Tooltip("Within this distance it notices the kid even from behind.")]
    [SerializeField] private float closeSenseDistance = 2.2f;
    [Tooltip("The kid counts as moving above this speed (m/s).")]
    [SerializeField] private float moveThreshold = 0.25f;
    [Tooltip("Seconds after the game starts before it can see anyone.")]
    [SerializeField] private float startDelay = 10f;

    [Header("Watching")]
    [SerializeField] private Vector2 watchTime = new Vector2(3f, 5.5f);
    [Tooltip("Time the kid gets to freeze after its eyes turn red.")]
    [SerializeField] private float reactionTime = 0.5f;
    [Tooltip("How long the kid must stay still during a chase before it stops to stare.")]
    [SerializeField] private float freezeToStopChase = 1.0f;
    [Tooltip("After losing interest, it won't stop to stare at a still kid for this long.")]
    [SerializeField] private float ignoreStillAfterWatch = 5f;

    [Header("Hearing")]
    [SerializeField] private float hearSprintDistance = 11f;
    [SerializeField] private float hearWalkDistance = 4f;

    [Header("Eyes")]
    [SerializeField, ColorUsage(false, true)] private Color calmEyes = new Color(0.25f, 0.01f, 0.005f);
    [SerializeField, ColorUsage(false, true)] private Color angryEyes = new Color(12f, 0.3f, 0.1f);
    [SerializeField] private float calmLight = 0.15f;
    [SerializeField] private float angryLight = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip chaseMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;
    [SerializeField] private AudioClip spottedSting;
    [SerializeField] private AudioClip jumpscareScream;
    [SerializeField] private AudioClip footstep;
    [Tooltip("Played on every other step while walking: the bad leg dragging.")]
    [SerializeField] private AudioClip dragStep;
    [SerializeField] private AudioClip growl;
    [Tooltip("Bone cracks when it twitches or snaps its head.")]
    [SerializeField] private AudioClip boneCrack;

    [Header("Creepy head")]
    [Tooltip("The head turns to follow the kid while it watches or chases.")]
    [SerializeField] private float headTrackLimit = 80f;
    [SerializeField] private Vector2 headSnapEvery = new Vector2(0.8f, 2.2f);

    public State CurrentState => state;
    public Transform FacePoint => eyePoint != null ? eyePoint : transform;

    private NavMeshAgent agent;
    private EscapeGameManager gm;
    private Transform player;
    private CharacterController playerCC;
    private ChildPlayerController playerMove;
    private Flashlight flashlight;

    private State state = State.Roam;
    private float stateTimer, watchDuration, pauseTimer, stepTimer, movingTime, stillTime, lostTime;
    private float ignoreStillUntil, startTime;
    private int waypointIndex = -1;
    private Vector3 lastSeen;
    private float eyeBlend;
    private Material eyeMat;
    private AudioSource music, sfx2D, steps, growlSource;
    private string currentAnim;
    private Transform headBone;
    private Vector3 headLocalForward;
    private float headWeight, snapTimer, snapAmount, snapTarget, crackTimer, stuckTimer, navRetryTimer;
    private bool stepToggle;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        gm = EscapeGameManager.Instance;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerCC = p.GetComponent<CharacterController>();
            playerMove = p.GetComponent<ChildPlayerController>();
        }
        startTime = Time.time;

        if (eyeRenderer != null)
        {
            eyeMat = eyeRenderer.material; // own copy so only this monster's eyes change
            eyeMat.EnableKeyword("_EMISSION");
        }

        music = NewSource("ChaseMusic", chaseMusic, true, 0f, 0f);
        sfx2D = NewSource("Stingers", null, false, 1f, 0f);
        steps = NewSource("Footsteps", null, false, 1f, 1f, 2f, 24f);
        growlSource = NewSource("Growl", growl, true, 0.55f, 1f, 1f, 10f);
        if (growl != null) growlSource.Play();
        if (chaseMusic != null) music.Play();

        if (animator != null)
        {
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { headBone = t; break; }
            if (headBone != null) headLocalForward = Quaternion.Inverse(headBone.rotation) * transform.forward;
        }
        crackTimer = Random.Range(4f, 9f);

        if (TryPlaceOnNavMesh()) EnterState(State.Roam);
    }

    // Puts the monster on the NavMesh. If the scene has no NavMesh near it, bakes one as a last resort.
    private bool TryPlaceOnNavMesh()
    {
        if (agent.isOnNavMesh) return true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas) ||
            NavMesh.SamplePosition(transform.position, out hit, 40f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }
        NavMeshSurface surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface != null)
        {
            Debug.LogWarning("MonsterAI: no NavMesh found, baking one now. Run Tools > Hospital Escape > Add or Reset Monster to bake it in the editor.", this);
            surface.BuildNavMesh();
            if (NavMesh.SamplePosition(transform.position, out hit, 40f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return agent.isOnNavMesh;
            }
        }
        Debug.LogError("MonsterAI: there is no NavMesh for the monster to walk on. Use Tools > Hospital Escape > Add or Reset Monster.", this);
        return false;
    }

    private AudioSource NewSource(string name, AudioClip clip, bool loop, float volume, float spatial, float minDist = 1f, float maxDist = 20f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1.5f;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = loop;
        a.volume = volume;
        a.spatialBlend = spatial;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = minDist;
        a.maxDistance = maxDist;
        a.playOnAwake = false;
        return a;
    }

    private void Update()
    {
        if (player == null) return;
        if (!agent.isOnNavMesh)
        {
            navRetryTimer -= Time.deltaTime;
            if (navRetryTimer <= 0f)
            {
                navRetryTimer = 1f;
                if (TryPlaceOnNavMesh()) EnterState(State.Roam);
            }
            return;
        }
        if (flashlight == null) flashlight = player.GetComponentInChildren<Flashlight>();

        bool over = gm != null && gm.IsOver;
        if (over && state != State.Attack)
        {
            agent.isStopped = true;
            SetAnim("Idle");
            UpdateEyesAndMusic(false);
            return;
        }

        // ---- what the monster knows this frame
        Vector3 pv = playerCC != null ? playerCC.velocity : Vector3.zero;
        pv.y = 0f;
        bool airborne = playerMove != null && !playerMove.IsGroundedStable;
        bool playerMoving = pv.magnitude > moveThreshold || airborne;
        movingTime = playerMoving ? movingTime + Time.deltaTime : 0f;
        stillTime = playerMoving ? 0f : stillTime + Time.deltaTime;
        bool sees = CanSeePlayer();
        if (sees) lastSeen = player.position;
        float dist = Vector3.Distance(transform.position, player.position);
        stateTimer += Time.deltaTime;

        switch (state)
        {
            case State.Roam:
            case State.Investigate:
            case State.Search:
                if (sees && (movingTime > 0.1f || Time.time > ignoreStillUntil))
                {
                    EnterState(State.Watch);
                    break;
                }
                if (Heard(dist))
                {
                    lastSeen = player.position;
                    EnterState(State.Investigate);
                }
                if (state == State.Roam) UpdateRoam();
                else UpdateGoToPoint();
                break;

            case State.Watch:
                FacePlayer();
                if (sees && stateTimer > reactionTime && movingTime > 0.12f)
                {
                    EnterState(State.Chase);
                    break;
                }
                lostTime = sees ? 0f : lostTime + Time.deltaTime;
                if (lostTime > 1.5f) { EnterState(State.Search); break; }
                if (stateTimer > watchDuration)
                {
                    // the kid stayed still: lose interest and wander off
                    ignoreStillUntil = Time.time + ignoreStillAfterWatch;
                    EnterState(State.Roam);
                }
                break;

            case State.Chase:
                lostTime = sees ? 0f : lostTime + Time.deltaTime;
                agent.SetDestination(sees ? player.position : lastSeen);
                if (dist <= catchDistance && stillTime < freezeToStopChase)
                {
                    EnterState(State.Attack);
                    break;
                }
                if (sees && stillTime >= freezeToStopChase)
                {
                    EnterState(State.Watch); // froze in time: it stops and stares... right there
                    break;
                }
                if (!sees && (lostTime > 6f || (!agent.pathPending && agent.remainingDistance < 0.6f)))
                    EnterState(State.Search);
                break;

            case State.Attack:
                FacePlayer();
                break;
        }

        UpdateEyesAndMusic(state == State.Watch || state == State.Chase || state == State.Attack);
        UpdateFootsteps();
        UpdateStuck();
        UpdateCracks();
    }

    // If it's supposed to be walking but hasn't moved for a while, pick somewhere else to go.
    private void UpdateStuck()
    {
        bool shouldMove = !agent.isStopped && (state == State.Roam || state == State.Investigate || state == State.Search || state == State.Chase)
                          && !agent.pathPending && agent.remainingDistance > 1f;
        stuckTimer = shouldMove && agent.velocity.sqrMagnitude < 0.04f ? stuckTimer + Time.deltaTime : 0f;
        if (stuckTimer < 2.5f) return;
        stuckTimer = 0f;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        if (state == State.Chase) agent.SetDestination(lastSeen);
        else EnterState(State.Roam);
    }

    private void UpdateCracks()
    {
        if (boneCrack == null || state == State.Chase || state == State.Attack) return;
        crackTimer -= Time.deltaTime;
        if (crackTimer > 0f) return;
        crackTimer = Random.Range(5f, 12f);
        steps.pitch = Random.Range(0.8f, 1.2f);
        steps.PlayOneShot(boneCrack, 0.8f);
    }

    // After the animation: turn the head towards the kid, with sudden unnatural snaps while it stares.
    private void LateUpdate()
    {
        if (headBone == null || player == null) return;

        float targetWeight = state == State.Watch || state == State.Attack ? 1f
                           : state == State.Chase ? 0.8f
                           : state == State.Investigate || state == State.Search ? 0.4f : 0f;
        headWeight = Mathf.MoveTowards(headWeight, targetWeight, Time.deltaTime * 3f);

        // snaps: the head jerks sideways, holds, then creeps back
        if (state == State.Watch)
        {
            snapTimer -= Time.deltaTime;
            if (snapTimer <= 0f)
            {
                snapTimer = Random.Range(headSnapEvery.x, headSnapEvery.y);
                snapTarget = Random.Range(-35f, 35f);
                snapAmount = snapTarget;
                if (boneCrack != null && Random.value < 0.6f) steps.PlayOneShot(boneCrack, 0.6f);
            }
        }
        else snapTarget = 0f;
        snapAmount = Mathf.MoveTowards(snapAmount, 0f, Time.deltaTime * 12f);
        if (headWeight <= 0.001f && Mathf.Abs(snapAmount) < 0.01f) return;

        Vector3 lookAt = state == State.Investigate || state == State.Search ? lastSeen + Vector3.up * 0.6f
                                                                            : player.position + Vector3.up * 0.7f;
        Vector3 toTarget = lookAt - headBone.position;
        Vector3 headFwd = headBone.rotation * headLocalForward;
        Quaternion delta = Quaternion.FromToRotation(headFwd, toTarget);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        angle = Mathf.Clamp(angle, -headTrackLimit, headTrackLimit) * headWeight;
        Quaternion look = Quaternion.AngleAxis(angle, axis);
        Quaternion roll = Quaternion.AngleAxis(snapAmount, toTarget.normalized);
        headBone.rotation = roll * look * headBone.rotation;
    }

    // ------------------------------------------------------------ states
    private void EnterState(State next)
    {
        state = next;
        stateTimer = 0f;
        lostTime = 0f;

        switch (next)
        {
            case State.Roam:
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = walkSpeed;
                pauseTimer = 0f;
                PickWaypoint(preferAwayFromPlayer: Time.time < ignoreStillUntil);
                break;

            case State.Investigate:
            case State.Search:
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = next == State.Investigate ? investigateSpeed : walkSpeed;
                agent.SetDestination(lastSeen);
                pauseTimer = 0f;
                break;

            case State.Watch:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updateRotation = false;
                watchDuration = Random.Range(watchTime.x, watchTime.y);
                SetAnim("Stare");
                if (spottedSting != null && eyeBlend < 0.5f) sfx2D.PlayOneShot(spottedSting, 0.9f);
                break;

            case State.Chase:
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = runSpeed;
                SetAnim("Run");
                break;

            case State.Attack:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updateRotation = false;
                SetAnim("Attack", 0.05f);
                if (music != null) music.Stop();
                if (jumpscareScream != null) sfx2D.PlayOneShot(jumpscareScream, 1f);
                if (gm != null) gm.OnPlayerCaught(this);
                break;
        }
    }

    private void UpdateRoam()
    {
        if (agent.pathPending) return;
        if (agent.remainingDistance > 0.5f)
        {
            SetAnim("Walk");
            return;
        }
        // arrived: stand and listen for a moment, then move on
        if (pauseTimer <= 0f) pauseTimer = Random.Range(pauseAtWaypoint.x, pauseAtWaypoint.y);
        SetAnim("Idle");
        pauseTimer -= Time.deltaTime;
        if (pauseTimer <= 0.01f) PickWaypoint(false);
    }

    private void UpdateGoToPoint()
    {
        if (agent.pathPending) return;
        if (agent.remainingDistance > 0.5f)
        {
            SetAnim("Walk");
            return;
        }
        SetAnim("Stare"); // look around where it heard or last saw the kid
        if (stateTimer > 3.5f) EnterState(State.Roam);
    }

    private void PickWaypoint(bool preferAwayFromPlayer)
    {
        if (waypoints == null || waypoints.Length == 0) return;
        int best = -1;
        float bestScore = float.MinValue;
        for (int tries = 0; tries < 4; tries++)
        {
            int i = Random.Range(0, waypoints.Length);
            if (i == waypointIndex && waypoints.Length > 1) continue;
            float score = preferAwayFromPlayer && player != null
                ? Vector3.Distance(waypoints[i].position, player.position)
                : Random.value;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) best = (waypointIndex + 1) % waypoints.Length;
        waypointIndex = best;

        Vector3 target = waypoints[best].position;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            target = hit.position;
        agent.SetDestination(target);
        SetAnim("Walk");
    }

    // ------------------------------------------------------------ senses
    private bool CanSeePlayer()
    {
        if (Time.time - startTime < startDelay) return false;

        Vector3 eye = FacePoint.position;
        Vector3 chest = player.position + Vector3.up * 0.55f;
        Vector3 toPlayer = chest - eye;
        float dist = toPlayer.magnitude;

        float range = viewDistance;
        if (flashlight != null && flashlight.IsOn) range += flashlightBonus;
        if (playerMove != null && playerMove.IsCrouching) range *= crouchMultiplier;
        if (dist > range) return false;

        Vector3 flatDir = toPlayer; flatDir.y = 0f;
        if (dist > closeSenseDistance && Vector3.Angle(transform.forward, flatDir) > viewAngle * 0.5f)
            return false;

        int mask = ~((1 << 2) | (1 << gameObject.layer)); // ignore decoration and other monsters
        Vector3 head = player.position + Vector3.up * 0.9f;
        return LineToPlayer(eye, chest, mask) || LineToPlayer(eye, head, mask);
    }

    private bool LineToPlayer(Vector3 from, Vector3 to, int mask)
    {
        Vector3 d = to - from;
        if (Physics.Raycast(from, d.normalized, out RaycastHit hit, d.magnitude + 0.3f, mask, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);
        return false;
    }

    private bool Heard(float dist)
    {
        if (playerMove == null || !playerMove.IsMoving) return false;
        if (playerMove.IsSprinting && dist < hearSprintDistance) return true;
        return !playerMove.IsCrouching && dist < hearWalkDistance;
    }

    // ------------------------------------------------------------ presentation
    private void FacePlayer()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
    }

    private void SetAnim(string name, float fade = 0.25f)
    {
        if (animator == null || currentAnim == name) return;
        currentAnim = name;
        animator.CrossFadeInFixedTime(name, fade);
    }

    private void UpdateEyesAndMusic(bool alert)
    {
        eyeBlend = Mathf.MoveTowards(eyeBlend, alert ? 1f : 0f, Time.deltaTime * (alert ? 5f : 0.7f));
        if (eyeMat != null) eyeMat.SetColor("_EmissionColor", Color.Lerp(calmEyes, angryEyes, eyeBlend));
        if (eyeLight != null) eyeLight.intensity = Mathf.Lerp(calmLight, angryLight, eyeBlend);

        if (music != null && music.clip != null)
        {
            float target = alert ? musicVolume : 0f;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.deltaTime * (alert ? 2f : 0.35f));
        }
    }

    private void UpdateFootsteps()
    {
        if (footstep == null) return;
        float speed = agent.velocity.magnitude;
        if (speed < 0.3f) { stepTimer = 0.1f; return; }
        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;
        bool running = speed > walkSpeed + 0.8f;
        stepTimer = running ? 0.28f : 0.8f;
        steps.pitch = Random.Range(0.85f, 1.05f);
        stepToggle = !stepToggle;
        if (!running && stepToggle && dragStep != null)
            steps.PlayOneShot(dragStep, 0.7f);   // the limping leg drags
        else
            steps.PlayOneShot(footstep, running ? 1f : 0.65f);
    }
}
