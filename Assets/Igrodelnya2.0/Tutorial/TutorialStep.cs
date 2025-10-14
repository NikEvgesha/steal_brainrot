using System;
using UnityEngine;

public class TutorialStep : MonoBehaviour 
{
    public Action<TutorialStep> StepEnd;
    public virtual void ActivateStep()
    {

    }
    public virtual void DeactivateStep()
    {
        StepEnd?.Invoke(this);
    }
}
