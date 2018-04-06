using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class ExtractionRope : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] models;

    [Header("Cargo")]
    public GameObject[] payloads;

    [Header("Extraction Chutes")]
    public GameObject[] chutes;
    public float windForce;

    [Header("Extraction Rope")]
    public GameObject ropeObject;
    public Material material;
    private ObiRope rope;

    [Header("Cargo Container")]
    public Transform container;

    private Rigidbody rbParachute, rbChuteBag, rbCargo;
    private bool refreshLoads, deploy, deployed, pullChute, payloadLaunched;
    private ObiCollider extractionCollider, cargoCollider, chuteObiCollider;

    // Use this for initialization
    void Start ()
    {
        refreshLoads = false;
        deploy = false;
        deployed = false;
        pullChute = false;
        payloadLaunched = false;
	}
	
	// Update is called once per frame
	void Update ()
    {
        HandleInput();

        if(deploy)
            if(rbChuteBag != null)
                rbChuteBag.AddForce(new Vector3(0, 0, -windForce));

        if (pullChute)
        {
            rbParachute.AddForce(new Vector3(0, 0, -windForce));
            rbParachute.transform.position += (new Vector3(0, 1.0f, 0) * Time.deltaTime);
        }

        if(payloadLaunched)
        {
            rbCargo.AddForce(new Vector3(0, 0, -windForce * 0.5f));
            //rbParachute.AddForce(new Vector3(0, 9.0f, 0.0f));
        }

        if ((payloads[0] != null) && deployed && (payloads[0].transform.position.z < -19.0f))
        {
            StartCoroutine(LaunchCargo());
            deployed = false;
            payloadLaunched = true;

        }

        if (chutes[1] != null && payloadLaunched)
        {
            if(payloads[0].transform.position.z <= chutes[1].transform.position.z)
            {
                payloadLaunched = false;
                chutes[1].transform.eulerAngles = new Vector3(90.0f, 0, 0);
                chutes[1].transform.parent = payloads[0].transform;
                rbParachute.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationY |
                 RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX;
            }
        }

    }

    void LoadObjects()
    {
        print("Load Objects");
        chutes[0] = (GameObject)Instantiate(models[0]);
        payloads[0] = (GameObject)Instantiate(models[1]);

        chutes[0].transform.position = new Vector3(0.0f, 0.0204f, -9.0f);
        payloads[0].transform.position = new Vector3(0.0f, 0.0f, 0.5f);

        chutes[0].transform.parent = container;
        payloads[0].transform.parent = container;
    }

    void HandleInput()
    {
        if (Input.GetButtonDown("1"))       // Load / Destroy objects
        {
            refreshLoads = !refreshLoads;
            if (refreshLoads)
                LoadObjects();
            else
                RemovePayloads();
        }

        if (Input.GetButtonDown("2") && (payloads[0] != null) && (chutes[0] != null) && (ropeObject == null))
                StartCoroutine(MakeRope());

        if (Input.GetButtonDown("3") && (chutes[1] == null) && (chutes[0] != null) && (ropeObject != null))
            StartCoroutine(DeployExtractionChute());

        if (chutes[1] != null)
        {
            if (chutes[1].transform.localScale.x < 1.0f)
                chutes[1].transform.localScale += new Vector3(1.0f, 1.0f, 1.0f) * 1.60f * Time.deltaTime;
            else
                chutes[1].transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        }

        if (payloads[0] != null)
        {
            if (Input.GetKey("left"))
                payloads[0].transform.position -= (5.0f * new Vector3(0, 0, 1) * Time.deltaTime);
            else if (Input.GetKey("right"))
                payloads[0].transform.position += (5.0f * new Vector3(0, 0, 1) * Time.deltaTime);
        }

        if(chutes[0] != null)
        {
            if (Input.GetKey("up"))
                chutes[0].transform.position -= (5.0f * new Vector3(0, 0, 1) * Time.deltaTime);
            else if (Input.GetKey("down"))
                chutes[0].transform.position += (5.0f * new Vector3(0, 0, 1) * Time.deltaTime);
        }
    }

    public IEnumerator MakeRope()
    {
        print("Load Extraction Rope");
        ropeObject = new GameObject("rope", 
                    typeof(ObiSolver),
                    typeof(ObiRope),
                    typeof(ObiCatmullRomCurve),
                    typeof(ObiRopeCursor));

        // get references to all components:
        ObiSolver solver = ropeObject.GetComponent<ObiSolver>();
        rope = ropeObject.GetComponent<ObiRope>();
        ObiCatmullRomCurve path = ropeObject.GetComponent<ObiCatmullRomCurve>();
        ObiRopeCursor cursor = ropeObject.GetComponent<ObiRopeCursor>();

        // set up component references
        rope.Solver = solver;
        rope.ropePath = path;
        rope.section = (ObiRopeSection)Resources.Load("DefaultRopeSection");
        rope.GetComponent<MeshRenderer>().material = material;
        //cursor.rope = rope;

        // Calculate rope start/end and direction in local space: (plus offset)
        Vector3 localStart = transform.InverseTransformPoint(chutes[0].transform.position + new Vector3(0, 0.2f, 0));
        Vector3 localEnd = transform.InverseTransformPoint(payloads[0].transform.position + new Vector3(0, 0.5f, 0));
        Vector3 direction = (localEnd - localStart).normalized;

        // Generate rope path:
        path.controlPoints.Clear();
        path.controlPoints.Add(localStart - direction);
        path.controlPoints.Add(localStart);
        path.controlPoints.Add(localEnd);
        path.controlPoints.Add(localEnd + direction);

        //correct thickness, particle resolution, and parenting
        rope.thickness = 0.025f;
        rope.resolution = 0.05f;
        rope.transform.parent = payloads[0].transform;

        yield return rope.StartCoroutine(rope.GeneratePhysicRepresentationForMesh());
        rope.AddToSolver(null);

        extractionCollider = new ObiCollider();
        cargoCollider = new ObiCollider();

        BoxCollider extractionBox = chutes[0].AddComponent<BoxCollider>();
        extractionBox.size = Vector3.zero;
        BoxCollider cargoBox = payloads[0].AddComponent<BoxCollider>();
        cargoBox.center = new Vector3(0.0f, 0.5f, 0.0f);

        extractionCollider = chutes[0].AddComponent<ObiCollider>();
        cargoCollider = payloads[0].AddComponent<ObiCollider>();

        // remove the constraints from the solver, because we cannot modify the constraints list while the solver is using it.
        rope.PinConstraints.RemoveFromSolver(null);
        ObiPinConstraintBatch constraintsBatch = rope.PinConstraints.GetFirstBatch();            
        constraintsBatch.AddConstraint(0, extractionCollider, Vector3.zero, 0.0f);
        constraintsBatch.AddConstraint(rope.UsedParticles - 1, cargoCollider, Vector3.zero, 0.0f);
        rope.PinConstraints.AddToSolver(null);
        rope.PinConstraints.PushDataToSolver();

    }

    IEnumerator DeployExtractionChute()
    {
        print("Deploy Extraction Chute");
        //add force component to extraction bag
        rbChuteBag = chutes[0].AddComponent<Rigidbody>();
        rbChuteBag.useGravity = true;
        rbChuteBag.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        deploy = true;

        yield return new WaitForSeconds(0.8f);

        pullChute = true;
        // remove the constraints from the solver, because we cannot modify the constraints list while the solver is using it.
        //detach rope from extraction bag
        ObiPinConstraintBatch constraintsBatch = rope.PinConstraints.GetFirstBatch();
        rope.PinConstraints.RemoveFromSolver(null);
        constraintsBatch.RemoveConstraint(0);
        rope.PinConstraints.AddToSolver(null);

        //create open parachute
        chutes[1] = (GameObject)Instantiate(models[2]);
        chutes[1].transform.parent = payloads[0].transform;
        chutes[1].transform.position = chutes[0].transform.position - new Vector3(0.0f, 0.0f, 0.0f);
        chutes[1].transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        //add box collider and obi collider components to parachute
        BoxCollider chuteBoxCollider = chutes[1].AddComponent<BoxCollider>();
        chuteBoxCollider.size = new Vector3(1.0f, 1.0f, 0.2f);
        chuteBoxCollider.center = new Vector3(0, 0, -0.1f);
        chuteObiCollider = new ObiCollider();
        chuteObiCollider = chutes[1].AddComponent<ObiCollider>();

        //attach rope to parachute
        constraintsBatch = rope.PinConstraints.GetFirstBatch();
        rope.PinConstraints.RemoveFromSolver(null);
        constraintsBatch.AddConstraint(0, chuteObiCollider, new Vector3(0,0,-0.1f), 0.0f);
        rope.PinConstraints.AddToSolver(null);
        rope.PinConstraints.PushDataToSolver();

        //extraction bag falls after parachute is attached
        rbChuteBag.useGravity = true;

        rbParachute = chutes[1].AddComponent<Rigidbody>();
        rbParachute.AddForce(0.0f, 0.0f, -10.0f);
        rbParachute.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY |
                 RigidbodyConstraints.FreezePositionY;

        yield return new WaitForSeconds(1);

        Destroy(chutes[0]);

        rbParachute.constraints = RigidbodyConstraints.FreezePositionX| RigidbodyConstraints.FreezeRotationY |
                 RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;

        deploy = false;
        deployed = true;
        pullChute = false;

        chutes[1].transform.parent = container;
    }

    IEnumerator LaunchCargo()
    {
        print("payload launched");
        rbCargo = payloads[0].AddComponent<Rigidbody>();
        rbCargo.useGravity = true;
        rbCargo.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        yield return new WaitForSeconds(1);

        //rbCargo.isKinematic = true;
    }

    void RemovePayloads()
    {
        print("Destroy Objects");
        if(payloads != null)
            for (int i = 0; i < 8; i++)
                if (payloads[i] != null)
                    Destroy(payloads[i]);

        if(chutes != null)
            for (int i = 0; i < 2; i++)
                if (chutes[i] != null)
                    Destroy(chutes[i]);

        if(rope != null)
            Destroy(ropeObject);

        deploy = false;
        pullChute = false;
        deployed = false;
        payloadLaunched = false;
    }
}
