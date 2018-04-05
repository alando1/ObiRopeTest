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

    [Header("Extraction Rope")]
    public GameObject ropeObject;
    public Material material;
    private ObiRope rope;

    [Header("Cargo Container")]
    public Transform container;

    private bool refreshLoads, deploy, deployed, pullChute;
    private ObiCollider extractionCollider, cargoCollider, chuteObiCollider;

    // Use this for initialization
    void Start ()
    {
        refreshLoads = false;
        deployed = false;
        pullChute = false;
	}
	
	// Update is called once per frame
	void Update ()
    {
        HandleInput();

        if(deploy)
            chutes[0].transform.position -= (20.0f * new Vector3(0, 0, 1) * Time.deltaTime);

        if (pullChute)
            chutes[1].transform.position -= (20.0f * new Vector3(0, 0, 1) * Time.deltaTime);
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
        deploy = true;
        print("Deploy Extraction Chute");
        chutes[1] = (GameObject)Instantiate(models[2]);
        chutes[1].transform.parent = payloads[0].transform;
        chutes[1].transform.position = chutes[0].transform.position;
        chutes[1].transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        yield return new WaitForSeconds(0.2f);
        pullChute = true;
        BoxCollider chuteBoxCollider = chutes[1].AddComponent<BoxCollider>();
        chuteBoxCollider.size = Vector3.zero;

        chuteObiCollider = new ObiCollider();
        chuteObiCollider = chutes[1].AddComponent<ObiCollider>();

        // remove the constraints from the solver, because we cannot modify the constraints list while the solver is using it.
        ObiPinConstraintBatch constraintsBatch = rope.PinConstraints.GetFirstBatch();
        rope.PinConstraints.RemoveFromSolver(null);
        constraintsBatch.RemoveConstraint(0);
        rope.PinConstraints.AddToSolver(null);
        ////constraintsBatch.AddConstraint(0, chuteObiCollider, Vector3.zero, 0.0f);
        ////  constraintsBatch.AddConstraint(rope.UsedParticles - 1, cargoCollider, Vector3.zero, 0.0f);
        //rope.PinConstraints.AddToSolver(null);
        //rope.PinConstraints.PushDataToSolver();

        constraintsBatch = rope.PinConstraints.GetFirstBatch();
        rope.PinConstraints.RemoveFromSolver(null);
        constraintsBatch.AddConstraint(0, chuteObiCollider, Vector3.zero, 0.0f);
        rope.PinConstraints.AddToSolver(null);
        rope.PinConstraints.PushDataToSolver();


        yield return new WaitForSeconds(1);
        deploy = false;
        deployed = true;

        yield return new WaitForSeconds(0.2f);
        pullChute = false;

    }

    void RemovePayloads()
    {
        print("Destroy Objects");
        for (int i = 0; i < 8; i++)
            if (payloads[i] != null)
                Destroy(payloads[i]);

        for (int i = 0; i < 2; i++)
            if (chutes[i] != null)
                Destroy(chutes[i]);

        Destroy(ropeObject);
        deploy = false;
        pullChute = false;
    }
}
