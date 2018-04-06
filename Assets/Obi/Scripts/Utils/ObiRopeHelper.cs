using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

//[RequireComponent(typeof(ObiRope))]
//[RequireComponent(typeof(ObiCatmullRomCurve))]
public class ObiRopeHelper : MonoBehaviour {

    [Header("Rope Settings")]
    public ObiSolver solver;
	public ObiRopeSection section;
	public Material material;
	private Transform start;
	private Transform end;

    [Header("Heavy Equipment Rope")]
    public ObiRope rope;

    [Header("End points")]
    public GameObject heavyCargoObject;
    public GameObject extractionObject;
    public GameObject ExtractionChute;
    public ObiCollider cargoCollider, extractionCollider;

    [Header("Models")]
    public GameObject [] models;
		
    private bool deploy;
    private ObiRope dynamicRope;
	private ObiCatmullRomCurve path;
    private ObiPinConstraints constraints;
    private ObiPinConstraintBatch constraintsBatch;
    private ObiDistanceConstraints distanceConstraints;

    private Vector3 initialExtractionPos;
    private Vector3 initialHeavyCargoPos;

    void Start ()
    {
        deploy = false;
    
        initialHeavyCargoPos = new Vector3(0.0f, 0.0f, 0.5f);
        initialExtractionPos = new Vector3(0.0f, 0.0204f, -9.0f);
    }

    private void Update()
    {

        if(Input.GetButtonDown("1"))
        {
            if (rope != null)
                DeleteObjects();
            else
                StartCoroutine(Setup());
        }

        if(Input.GetKey("left"))
            heavyCargoObject.transform.position -= (0.05f * new Vector3(0, 0, 100) * Time.deltaTime);
        else if(Input.GetKey("right"))
            heavyCargoObject.transform.position += (0.05f * new Vector3(0, 0, 100) * Time.deltaTime);

        if (Input.GetKey("up"))
            extractionObject.transform.position -= (0.05f * new Vector3(0, 0, 100) * Time.deltaTime);
        else if (Input.GetKey("down"))
            extractionObject.transform.position += (0.05f * new Vector3(0, 0, 100) * Time.deltaTime);


        if (Input.GetButtonDown("2"))
        {
            deploy = !deploy;

            if (deploy)
                StartCoroutine(DeployExtractionChute());
        }

        if(deploy)
        {
            extractionObject.transform.position -= (0.2f * new Vector3(0, 0, 100) * Time.deltaTime);
        }

    }

    IEnumerator Setup()
    {
        extractionObject = (GameObject)Instantiate(models[0]);
        heavyCargoObject = (GameObject)Instantiate(models[1]);

        extractionObject.transform.position = initialExtractionPos;
        heavyCargoObject.transform.position = initialHeavyCargoPos;

        start = extractionObject.transform;
        end = heavyCargoObject.transform;

        // Get all needed components and interconnect them:
        rope = GetComponent<ObiRope>();
        path = GetComponent<ObiCatmullRomCurve>();
        rope.Solver = (ObiSolver)Instantiate(solver);
        rope.ropePath = path;
        rope.section = section;
        GetComponent<MeshRenderer>().material = material;

        // Calculate rope start/end and direction in local space: (plus offset)
        Vector3 localStart = transform.InverseTransformPoint(start.position + new Vector3(0, 0.2f, 0));
        Vector3 localEnd = transform.InverseTransformPoint(end.position + new Vector3(0, 0.5f, 0));
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
        rope.transform.parent = heavyCargoObject.transform;

        // Generate particles and add them to solver:
        yield return StartCoroutine(rope.GeneratePhysicRepresentationForMesh());
		rope.AddToSolver(null);

		// Fix first and last particle in place:
		//rope.invMasses[0] = 0;
		//rope.invMasses[rope.UsedParticles-1] = 0;
		//Oni.SetParticleInverseMasses(solver.OniSolver,new float[]{0},1,rope.particleIndices[0]);
		//Oni.SetParticleInverseMasses(solver.OniSolver,new float[]{0},1,rope.particleIndices[rope.UsedParticles-1]);

        constraints = rope.GetComponent<ObiPinConstraints>();
        constraintsBatch = rope.PinConstraints.GetFirstBatch();            

        extractionCollider = new ObiCollider();
        cargoCollider = new ObiCollider();

        BoxCollider extractionBox = extractionObject.AddComponent<BoxCollider>();
        extractionBox.size = Vector3.zero;
        BoxCollider cargoBox = heavyCargoObject.AddComponent<BoxCollider>();
        cargoBox.center = new Vector3(0.0f, 0.5f, 0.0f);

        extractionCollider = extractionObject.AddComponent(typeof(ObiCollider)) as ObiCollider;
        cargoCollider = heavyCargoObject.AddComponent(typeof(ObiCollider)) as ObiCollider;

        // remove the constraints from the solver, because we cannot modify the constraints list while the solver is using it.
        constraints.RemoveFromSolver(null);
        constraintsBatch.AddConstraint(0, extractionCollider, Vector3.zero, 0.0f);
        constraintsBatch.AddConstraint(rope.UsedParticles - 1, cargoCollider, Vector3.zero, 0.0f);  
        constraints.AddToSolver(null);
        constraints.PushDataToSolver();

    }

    IEnumerator DeployExtractionChute()
    {
        yield return new WaitForSeconds(1.5f);
        deploy = !deploy;
    }

    void DeleteObjects()
    {
        Destroy(extractionObject);
        Destroy(heavyCargoObject);
        Destroy(rope);
        Destroy(extractionCollider);
        Destroy(cargoCollider);
        //AddComponent<ObiRope>(typeof(ObiRope));
    }
		
}
