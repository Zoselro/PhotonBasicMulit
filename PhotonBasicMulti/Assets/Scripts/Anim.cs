using UnityEngine;

[System.Serializable]
public class Anim
{
    public AnimationClip Idle;
    public AnimationClip Move;
    public AnimationClip Attack1;
    public AnimationClip Attack2;
    public AnimationClip Skill1;
    public AnimationClip Skill2;
    public AnimationClip Die;
}

public enum AnimState
{
    idle,
    move,
    trace,
    attack,
    skill,
    die,
    move_L,
    move_R,
    move_B,
    count,
    dodge,
    jump,
    damage
}