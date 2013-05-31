using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Simple FSM
/// 
/// Idea is to start long running and repetitive tasks when we enter a state.
/// Implement tasks using coroutines.
/// Problem: need to interrupt these tasks when we change state!
/// Solution: Use coroutine manager (Prime31) to stop coroutines
/// 
/// </summary>

	

public class SimpleFSM : MonoBehaviour {
	
	private NavMeshAgent guard;	
	private Transform _transform;
	
	private Job hear;
	private Job see;	
	private Job patrol;	
	private Job investigate;
	
	private BotFreeMovementMotor guard_motor;
	
	private float patrolSpeed = 3.5f;
	private float alertSpeed = 5.0f;
	
	private float playerSpeed = 0f;
	
	private Vector3 playerPosition;
	
	private Vector3 suspiciousPosition;
	
	public Transform player;	
	public Transform[] patrolPoints;
		
	public enum State
	{
		Patrol, //= green
		Investigate, // = yellow
		Alert // = red
	}
	
	private State _state;
	public State state
	{
		get
		{
			return _state;
		}
		set
		{
			ExitState(_state);
			_state = value;
			EnterState(_state);
		}
	}

	void Awake()
	{		
		guard = GetComponent<NavMeshAgent>();
		guard.updateRotation = false;
		
		guard_motor = GetComponent<BotFreeMovementMotor>();
		
		_transform = transform;
		
		state = State.Patrol;
	}
	
	void EnterState(State stateEntered)
	{
		switch(stateEntered)
		{
		case State.Patrol:
			
			_transform.renderer.material.color = Color.green;
			
			patrol = new Job(Patrolling(),true);
			
			see = new Job(Seeing(player, 45f,60f,10f,0.5f,true,
				() => 
				{	
					Debug.Log ("Saw you!");
					
					state = State.Alert;
			
				}),true); 
			
			hear = new Job(Hearing(
				() =>
				{
					Debug.Log ("What was that?");
					
					state = State.Investigate;
			
				}),true);
			
			break;
			
		case State.Investigate:
			
			_transform.renderer.material.color = Color.yellow;
			
			investigate = new Job(Investigating(
				() =>
				{
					state = State.Patrol;
				}
				),true);
			
			see = new Job(Seeing(player, 45f,60f,10f,0.5f,true,
				() => 
				{	
					Debug.Log ("Saw you!");
					
					state = State.Alert;
			
				}),true);			
			
			break;
			
		case State.Alert:
			
			guard.speed = alertSpeed;
			
			_transform.renderer.material.color = Color.red;
			
			see = new Job(Seeing(player, 45f,60f,10f,0.5f,false,
				() => 
				{	
					Debug.Log ("Where you gone?");
					
					state = State.Investigate;
			
				}),true);				
			
			break;
		}
	}
	
	void ExitState(State stateExited)
	{
		switch(stateExited)
		{
		case State.Patrol:
			
			if(patrol != null) patrol.Kill();
			if(see != null) see.Kill();
			if(hear != null) hear.Kill();
			
			break;
		case State.Investigate:
			
			if(investigate != null) investigate.Kill();
			if(see != null) see.Kill();
			
			break;
		case State.Alert:
			
			if(see != null) see.Kill();
			
			break;
		}
	}	
	
	void Update()
	{		
		switch(state)
		{
		case State.Alert:
			
			guard.SetDestination(player.position);
			guard_motor.facingDirection = player.position - _transform.position;
			
			break;
		}
	}	
	
	void LateUpdate()
	{		
		//Calculate player velocity
		Vector3 vel = (playerPosition - player.position)/Time.deltaTime;
		
		playerPosition = player.position;
		
		playerSpeed = vel.magnitude;
	}
	
	IEnumerator Patrolling()
	{
		int i = 0;
		
		while(true)
		{
			guard.speed = patrolSpeed;
			
			guard.SetDestination(patrolPoints[i].position);
			
			while((_transform.position - guard.destination).sqrMagnitude > 2f)
			{
				guard_motor.facingDirection = patrolPoints[i].position - _transform.position;
				yield return null;
			}
			
			if(i == patrolPoints.Length - 1)
				i = 0;
			else
				++i;			
			
			guard_motor.facingDirection = patrolPoints[i].position - _transform.position;
			
			guard.speed = 0f;
			
			yield return new WaitForSeconds(1f);			
		}
	}
	
	IEnumerator Investigating(Action OnComplete)
	{
		suspiciousPosition = player.position;
		
		while(true)
		{
			guard_motor.facingDirection = suspiciousPosition - _transform.position;
			
			guard.speed = 0f;
			
			yield return new WaitForSeconds(1f);			
			
			guard.SetDestination(suspiciousPosition);
			
			guard.speed = alertSpeed;
						
			while((_transform.position - guard.destination).sqrMagnitude > 2f)
			{
				guard_motor.facingDirection = guard.desiredVelocity;
				yield return null;
			}  
			
			guard.speed = 0f;
			
			yield return new WaitForSeconds(1f);
			
			if(OnComplete != null) OnComplete();
		}
	}
	
	
	IEnumerator Seeing(Transform target, float angle, float distance, float maxHeight, float time, bool inRange, Action OnComplete)
	{		
		while(true)
		{		
			float timer = 0f;
			
			if(inRange)
			{
				while(IsInFov(target, angle, maxHeight) && (VisionCheck(target,distance)) && timer < time) 
				{			
					timer += Time.deltaTime;			
					yield return null;			
				}
			}
			else if(!inRange)
			{
				while((!IsInFov(target, angle, maxHeight) || !VisionCheck(target,distance)) && timer < time) 
				{			
					timer += Time.deltaTime;			
					yield return null;			
				}			
			}
			
			if(timer > time && OnComplete != null) OnComplete(); 
			
			yield return null;
		}
	}	
	
	IEnumerator Hearing(Action onComplete)
	{
		while(true)
		{			
			float hearingRange = 10f;

			bool heardNoise = false;
			
			while(!heardNoise && (_transform.position - player.position).sqrMagnitude < hearingRange*hearingRange && playerSpeed > 20f)
			{
				heardNoise = true;
			}
				
			if(heardNoise && onComplete != null) onComplete();
			
			yield return null;
		}
	}	
	
	
	public bool VisionCheck(Transform target, float distance)
	{
		RaycastHit hit;

		if(Physics.Raycast(_transform.position, target.position-_transform.position,out hit,distance))
		{
			if(hit.transform == player) return true;
			else return false;
		}
		else return false;
	}	
	
	public bool IsInFov(Transform target, float angle, float maxHeight)
	{
		var relPos = target.position - _transform.position; 
		float height = relPos.y;
		relPos.y = 0;
		
		if(Mathf.Abs(Vector3.Angle(relPos,transform.forward)) < angle)
		{
			if(Mathf.Abs(height) < maxHeight)
			{				
				return true;
			}
			else
			{				
				return false;
			}			
		}
		else return false;
	}		
	
}
