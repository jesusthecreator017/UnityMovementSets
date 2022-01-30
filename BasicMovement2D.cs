using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicMovement2D : MonoBehaviour
{

    [Header("Assignables")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private BoxCollider2D bc2;
    
    [Header("Movement Variables")]
    public float moveSpeed = 8.0f;
    private float move;

    [Header("Jump")]
    public float jumpForce;
    public bool isGrounded;
    public bool isJumping;

    [Header("Checks")]
    public Transform groundCheckPoint;
    public Vector2 groundCheckSize;

    [Header("LayersMasks")]
    public LayerMask groundLayer;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        bc2 = GetComponent<BoxCollider2D>();
    }

    // Update is called once per frame
    void Update()
    {
        move = Input.GetAxisRaw("Horizontal");
        isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0, groundLayer);

        if(rb.velocity.y < 0){
            isJumping = false;
        }

        if(Input.GetKey(KeyCode.Space)){
            if(isGrounded){
                Jump();
            }
        }
    }

    void FixedUpdate(){
        rb.velocity = new Vector2(move * moveSpeed, rb.velocity.y);
    }

    void Jump(){
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        isJumping = true;
    }
}
