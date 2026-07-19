using UnityEngine;

public class BillBoard : MonoBehaviour
{
    Transform m_CameraTr = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_CameraTr = Camera.main.transform;
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.forward = m_CameraTr.forward;  //ºôº¸µå Ã³¸®
    }
}
