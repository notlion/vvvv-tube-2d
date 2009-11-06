#region licence/info

//////project name
//vvvv plugin template

//////description
//basic vvvv node plugin template.
//Copy this an rename it, to write your own plugin node.

//////licence
//GNU Lesser General Public License (LGPL)
//english: http://www.gnu.org/licenses/lgpl.html
//german: http://www.gnu.de/lgpl-ger.html

//////language/ide
//C# sharpdevelop

//////dependencies
//VVVV.PluginInterfaces.V1;
//VVVV.Utils.VColor;
//VVVV.Utils.VMath;

//////initial author
//vvvv group

#endregion licence/info

//use what you need
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Collections.Generic;
using System.Collections;

using VVVV.PluginInterfaces.V1;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using SlimDX;
using SlimDX.Direct3D9;

//the vvvv node namespace
namespace VVVV.Nodes
{
	//class definition
	public class Tube2d: IPlugin, IDisposable, IPluginDXMesh
	{
		#region field declaration
		

		//the host (mandatory)
		private IPluginHost FHost;
		//Track whether Dispose has been called.
		private bool FDisposed = false;
		
        //inputs
        private IValueIn VertsIn;
        private IValueIn ThicknessIn;
        private IValueIn BinSizeIn;

		//a mesh output pin
		private IDXMeshOut MeshOut;

		//a list that holds a mesh for every device
		private Dictionary<int, Mesh> FDeviceMeshes = new Dictionary<int, Mesh>();


        protected bool update = false;

        protected int numVerts = 0;
        protected int numVertsOut = 0;
        protected int numThickness = 0;
        protected int numBins = 0;
        protected int numIndices = 0;

        protected bool zeroBins = false;

        protected Vector3[] verts;
        protected float[] thickness;
        protected int[] binSize;

        protected sVxBuffer[] VxBuffer;
        protected short[] IxBuffer;

        protected DataStream sVx, sIx;


		#endregion field declaration
		
		#region constructor/destructor
		
		public Tube2d()
		{
			//the nodes constructor
			//nothing to declare for this node
		}
		
		// Implementing IDisposable's Dispose method.
		// Do not make this method virtual.
		// A derived class should not be able to override this method.
		public void Dispose()
		{
			Dispose(true);
			// Take yourself off the Finalization queue
			// to prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}
		
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
		protected virtual void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if(!FDisposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					
				}
				// Release unmanaged resources. If disposing is false,
				// only the following code is executed.

				FHost.Log(TLogType.Debug, "PluginMeshTemplate is being deleted");
				
				// Note that this is not thread safe.
				// Another thread could start disposing the object
				// after the managed resources are disposed,
				// but before the disposed flag is set to true.
				// If thread safety is necessary, it must be
				// implemented by the client.
			}
			FDisposed = true;
		}

		// Use C# destructor syntax for finalization code.
		// This destructor will run only if the Dispose method
		// does not get called.
		// It gives your base class the opportunity to finalize.
		// Do not provide destructors in types derived from this class.
		~Tube2d()
		{
			// Do not re-create Dispose clean-up code here.
			// Calling Dispose(false) is optimal in terms of
			// readability and maintainability.
			Dispose(false);
		}
		#endregion constructor/destructor
		
		#region node name and info
		
		//provide node infos
		private static IPluginInfo FPluginInfo;
		public static IPluginInfo PluginInfo
		{
			get
			{
				if (FPluginInfo == null)
				{
					//fill out nodes info
					//see: http://www.vvvv.org/tiki-index.php?page=Conventions.NodeAndPinNaming
					FPluginInfo = new PluginInfo();
					
					//the nodes main name: use CamelCaps and no spaces
					FPluginInfo.Name = "Tube2d";
					//the nodes category: try to use an existing one
					FPluginInfo.Category = "Mesh";
					//the nodes version: optional. leave blank if not
					//needed to distinguish two nodes of the same name and category
					FPluginInfo.Version = "";
					
					//the nodes author: your sign
					FPluginInfo.Author = "ryan alexander";
					//describe the nodes function
					FPluginInfo.Help = "Offers a basic code layout to start from when writing a vvvv plugin";
					//specify a comma separated list of tags that describe the node
					FPluginInfo.Tags = "";
					
					//give credits to thirdparty code used
					FPluginInfo.Credits = "";
					//any known problems?
					FPluginInfo.Bugs = "";
					//any known usage of the node that may cause troubles?
					FPluginInfo.Warnings = "";
					
					//leave below as is
					System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
					System.Diagnostics.StackFrame sf = st.GetFrame(0);
					System.Reflection.MethodBase method = sf.GetMethod();
					FPluginInfo.Namespace = method.DeclaringType.Namespace;
					FPluginInfo.Class = method.DeclaringType.Name;
					//leave above as is
				}
				return FPluginInfo;
			}
		}

		public bool AutoEvaluate
		{
			//return true if this node needs to calculate every frame even if nobody asks for its output
			get {return false;}
		}
		
		#endregion node name and info
		
		#region pin creation
		
		//this method is called by vvvv when the node is created
		public void SetPluginHost(IPluginHost Host)
		{
			//assign host
			FHost = Host;
			
            //create inputs
            FHost.CreateValueInput("Vertices", 3, null, TSliceMode.Dynamic, TPinVisibility.True, out VertsIn);
            VertsIn.SetSubType3D(double.MinValue, double.MaxValue, 0.01, 0, 0, 0, false, false, false);

            FHost.CreateValueInput("Thickness", 1, null, TSliceMode.Dynamic, TPinVisibility.True, out ThicknessIn);
            ThicknessIn.SetSubType(double.MinValue, double.MaxValue, 0.01, 0.1, false, false, false);
            
            FHost.CreateValueInput("Bin Size", 1, null, TSliceMode.Dynamic, TPinVisibility.True, out BinSizeIn);
            BinSizeIn.SetSubType(-1, int.MaxValue, 1, -1, false, false, true);

			//create outputs
			FHost.CreateMeshOutput("Mesh", TSliceMode.Dynamic, TPinVisibility.True, out MeshOut);
		}

		#endregion pin creation
		
		#region mainloop
		
		public void Configurate(IPluginConfig Input)
		{
			//nothing to configure in this plugin
			//only used in conjunction with inputs of type cmpdConfigurate
		}
		
		//here we go, thats the method called by vvvv each frame
		//all data handling should be in here
		public void Evaluate(int SpreadMax)
		{
			MeshOut.SliceCount = 1;
            
            if (VertsIn.SliceCount != numVerts)
            {
                numVerts = VertsIn.SliceCount;
                numVertsOut = numVerts * 2;
                verts = new Vector3[numVerts];
                update = true;
            }

            if (ThicknessIn.SliceCount != numThickness)
            {
                numThickness = ThicknessIn.SliceCount;
                thickness = new float[numThickness];
                update = true;
            }

            if (BinSizeIn.SliceCount != numBins)
            {
                numBins = BinSizeIn.SliceCount;
                binSize = new int[numBins];
                update = true;
                //FHost.Log(TLogType.Message, numBins + " bins");
            }

            if (VertsIn.PinIsChanged)
            {
                double x, y, z;
                for (int i = 0; i < numVerts; i++)
                {
                    VertsIn.GetValue3D(i, out x, out y, out z);
                    verts[i] = new Vector3((float)x, (float)y, (float)z);
                }
                update = true;
            }

            if (ThicknessIn.PinIsChanged)
            {
                double th;
                for (int i = 0; i < numThickness; i++)
                {
                    ThicknessIn.GetValue(i, out th);
                    thickness[i] = (float)th;
                }
                update = true;
            }

            if (BinSizeIn.PinIsChanged)
            {
                zeroBins = true;
                double bs;
                for (int i = 0; i < numBins; i++)
                {
                    BinSizeIn.GetValue(i, out bs);
                    binSize[i] = (int)bs;
                    if (binSize[i] != 0)
                        zeroBins = false;
                    //FHost.Log(TLogType.Message, "bin "+i+" "+binSize[i]);
                }
                update = true;
            }

            if (update)
            {
                UpdateBuffers();
            }
		}
		
		#endregion mainloop
		
		#region DXMesh

        private void RemoveResource(int OnDevice)
        {
            Mesh m = FDeviceMeshes[OnDevice];
            //FHost.Log(TLogType.Debug, "Destroying Resource...");
            FDeviceMeshes.Remove(OnDevice);
            m.Dispose();
        }

		public void UpdateResource(IPluginOut ForPin, int OnDevice)
		{
			//Called by the PluginHost every frame for every device. Therefore a plugin should only do 
			//device specific operations here and still keep node specific calculations in the Evaluate call.

            try
            {
                Mesh mrty = FDeviceMeshes[OnDevice];
                if (update)
                    RemoveResource(OnDevice);
            }
            catch
            {
                update = true;
            }

            if (update)
            {
                Device dev = Device.FromPointer(new IntPtr(OnDevice));
                try
                {
                    Mesh nuMesh = new Mesh(dev, numIndices / 3, numVertsOut, MeshFlags.Dynamic | MeshFlags.WriteOnly, VertexFormat.PositionNormal);

                    sVx = nuMesh.LockVertexBuffer(LockFlags.Discard);
                    sIx = nuMesh.LockIndexBuffer(LockFlags.Discard);

                    unsafe
                    {
                        fixed (sVxBuffer* FixTemp = &VxBuffer[0])
                        {
                            IntPtr VxPointer = new IntPtr(FixTemp);
                            sVx.WriteRange(VxPointer, sizeof(sVxBuffer) * numVertsOut);
                        }
                        fixed (short* FixTemp = &IxBuffer[0])
                        {
                            IntPtr IxPointer = new IntPtr(FixTemp);
                            sIx.WriteRange(IxPointer, sizeof(short) * numIndices);
                        }
                    }

                    nuMesh.UnlockVertexBuffer();
                    nuMesh.UnlockIndexBuffer();

                    FDeviceMeshes.Add(OnDevice, nuMesh);
                }
                finally
                {
                    dev.Dispose();
                    update = false;
                }
            }
		}
		
		public void DestroyResource(IPluginOut ForPin, int OnDevice, bool OnlyUnManaged)
		{
			//Called by the PluginHost whenever a resource for a specific pin needs to be destroyed on a specific device. 
			//This is also called when the plugin is destroyed, so don't dispose dxresources in the plugins destructor/Dispose()

			try
			{
				RemoveResource(OnDevice);
			}
			catch
			{
				//resource is not available for this device. good. nothing to do then.
			}
		}
		
		public void GetMesh(IDXMeshOut ForPin, int OnDevice, out int MeshPointer)
		{
			// Called by the PluginHost everytime a mesh is accessed via a pin on the plugin.
			// This is called from the PluginHost from within DirectX BeginScene/EndScene,
			// therefore the plugin shouldn't be doing much here other than handing back the right mesh

			MeshPointer = 0;
			//in case the plugin has several mesh outputpins a test for the pin can be made here to get the right mesh.
			if (ForPin == MeshOut)
			{
				Mesh m = FDeviceMeshes[OnDevice];
				if (m != null)
					MeshPointer = m.ComPointer.ToInt32();
			}
		}

		#endregion


        protected void UpdateBuffers()
        {
            List<short> idxTemp = new List<short>();
            List<sVxBuffer> vtxTemp = new List<sVxBuffer>();

            int i = 0;
            while (i < numVerts && !zeroBins)
            {
                for (int j = 0; j < numBins && i < numVerts; j++)
                {
                    int bs = binSize[j];
                    for (int k = 0, n = bs < 0 ? numVerts : bs; k < n && i < numVerts; k++)
                    {
                        bool nextSafe = k + 1 < n && i + 1 < numVerts;
                        
                        // normal

                        Vector3 normal = new Vector3();
                        if (k > 0)
                        {
                            normal.X -= verts[i].Y - verts[i - 1].Y;
                            normal.Y += verts[i].X - verts[i - 1].X;
                        }
                        if (nextSafe)
                        {
                            normal.X -= verts[i + 1].Y - verts[i].Y;
                            normal.Y += verts[i + 1].X - verts[i].X;
                        }
                        normal.Normalize();

                        // vertex and indices

                        vtxTemp.Add(new sVxBuffer(
                            verts[i] - normal * thickness[i % numThickness],
                            new Vector3(0, 0, 1)
                        ));
                        vtxTemp.Add(new sVxBuffer(
                            verts[i] + normal * thickness[i % numThickness],
                            new Vector3(0, 0, 1)
                        ));

                        if (nextSafe)
                        {
                            int i2 = i * 2;
                            idxTemp.Add((short)i2);
                            idxTemp.Add((short)(i2 + 1));
                            idxTemp.Add((short)(i2 + 3));
                            idxTemp.Add((short)(i2 + 3));
                            idxTemp.Add((short)(i2 + 2));
                            idxTemp.Add((short)i2);
                        }

                        i++;
                    }
                }
            }

            VxBuffer = vtxTemp.ToArray();
            IxBuffer = idxTemp.ToArray();
            numVertsOut = VxBuffer.Length;
            numIndices = IxBuffer.Length;
        }


        public struct sVxBuffer
        {
            public Vector3 Vel;
            public Vector3 Nel;

            public sVxBuffer(Vector3 v, Vector3 n)
            {
                this.Vel = v;
                this.Nel = n;
            }
        }
    }
}
