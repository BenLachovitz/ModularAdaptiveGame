using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMotion : MonoBehaviour
{
    private CharacterController controller;
    private float speed = 20;
    public GameObject camera; // must be connected in UNITY


    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        float dx, dz;
        float rotation_around_Y;
        float rotation_around_X;

        // motion
        rotation_around_Y = Input.GetAxis("Mouse X"); // horizontal mouse motion
        transform.Rotate(new Vector3(0, rotation_around_Y, 0));

        rotation_around_X = -Input.GetAxis("Mouse Y");// vertical mouse motion
        camera.transform.Rotate(new Vector3(rotation_around_X, 0, 0));

        dz = Input.GetAxis("Vertical");// can be -1 , 0 , 1
        dx = Input.GetAxis("Horizontal");// can be -1 , 0 , 1

        Vector3 motion = new Vector3(dx * speed * Time.deltaTime, -0.1f,
                                dz * speed * Time.deltaTime);
        motion = transform.TransformDirection(motion); // transformation from local to global coordinates
        controller.Move(motion); // motion is vector in global coordinates
    }


}