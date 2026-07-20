using UnityEngine;

public class MonsterEvent : MonoBehaviour
{
    Monster_Ctrl m_RefMonCS;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_RefMonCS = transform.parent.GetComponent<Monster_Ctrl>(); 
    }

    //// Update is called once per frame
    //void Update()
    //{
        
    //}

    void Event_AttHit()
    {
        //m_RefMonCS.Event_AttHit();
    }
}
