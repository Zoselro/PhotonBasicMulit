using UnityEngine;
using UnityEngine.UI;

public class BillBoard : MonoBehaviour
{
    Transform m_CameraTr = null;

    void Start()
    {
        m_CameraTr = Camera.main.transform;
    }

    void Update()
    {
        this.transform.forward = m_CameraTr.forward;  //ºôº¸µå Ã³¸®
    }
}
