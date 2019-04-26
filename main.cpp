#include <igl/readOFF.h>
#include <igl/readSTL.h>
#include <igl/writeSTL.h>
//#undef IGL_STATIC_LIBRARY
#include <igl/copyleft/cgal/mesh_boolean.h>
#include <igl/opengl/glfw/Viewer.h>

#include <Eigen/Core>
#include <iostream>

#include "tutorial_shared_path.h"



//#include <igl/barycenter.h>
//#include <igl/boundary_facets.h>
//#include <igl/copyleft/tetgen/tetrahedralize.h>
//#include <igl/copyleft/tetgen/cdt.h>
//#include <igl/winding_number.h>
//#include <igl/unique_simplices.h>
//#include <igl/remove_unreferenced.h>
//#include <igl/copyleft/cgal/remesh_self_intersections.h>
//#include <igl/redrum.h>





#include <Windows.h>

#define DBOUT( s )            \
{                             \
   std::ostringstream os_;    \
   os_ << s;                   \
   OutputDebugString( os_.str().c_str() );  \
}

Eigen::MatrixXd VA, VB, VC;
Eigen::MatrixXd NA, NB, NC;
Eigen::VectorXi J, I;
Eigen::MatrixXi FA, FB, FC;
igl::MeshBooleanType boolean_type(
	igl::MESH_BOOLEAN_TYPE_UNION);

void update(igl::opengl::glfw::Viewer &viewer)
{
	Eigen::MatrixXd C(FC.rows(), 3);
	for (size_t f = 0; f < C.rows(); f++)
	{
		if (J(f) < FA.rows())
		{
			C.row(f) = Eigen::RowVector3d(1, 0, 0);
		}
		else
		{
			C.row(f) = Eigen::RowVector3d(0, 1, 0);
		}
	}
	viewer.data().clear();
	viewer.data().set_mesh(VC, FC);
	viewer.data().set_colors(C);
}

extern "C"
{
	__declspec(dllexport) void DoBooleanOperation(double* pVA, int sizeVA, int* pFA, int sizeFA,
		double* pVB, int sizeVB, int* pFB, int sizeFB,
		int opperation,
		double** ppVC, int* pSizeVC, int** ppFC, int* pSizeFC)
	{
		using namespace Eigen;

		igl::MeshBooleanType booleanType(igl::MESH_BOOLEAN_TYPE_UNION);
		if (opperation == 1)
		{
			booleanType = igl::MESH_BOOLEAN_TYPE_MINUS;
		}
		else if (opperation == 2)
		{
			booleanType = igl::MESH_BOOLEAN_TYPE_INTERSECT;
		}

		// make local data to hold our arrays
		Eigen::MatrixXd VA, VB, VC;
		Eigen::MatrixXi FA, FB, FC;
		VA = Map<Matrix<double, Dynamic, Dynamic, RowMajor> >(pVA, sizeVA / 3, 3);
		FA = Map<Matrix<int, Dynamic, Dynamic, RowMajor> >(pFA, sizeFA / 3, 3);
		VB = Map<Matrix<double, Dynamic, Dynamic, RowMajor> >(pVB, sizeVB / 3, 3);
		FB = Map<Matrix<int, Dynamic, Dynamic, RowMajor> >(pFB, sizeFB / 3, 3);

		//DBOUT("The value of x is " << VA);

		igl::copyleft::cgal::mesh_boolean(VA, FA, VB, FB, booleanType, VC, FC, J);

		//igl::writeSTL("c:\\Temp\\test.stl", VC, FC, true);

		int count = VC.rows() * VC.cols();
		*pSizeVC = count;
		*ppVC = new double[count];
		int VCcols = VC.cols();
		for (int row = 0; row < VC.rows(); row++)
		{
			for (int col = 0; col < VCcols; col++)
			{
				ppVC[0][row*VCcols + col] = VC(row, col);
			}
		}

		count = FC.rows() * FC.cols();
		*pSizeFC = count;
		*ppFC = new int[count];
		int FCcols = FC.cols();
		for (int row = 0; row < FC.rows(); row++)
		{
			for (int col = 0; col < FCcols; col++)
			{
				ppFC[0][row*FCcols + col] = FC(row, col);
			}
		}
	}

	__declspec(dllexport) void DeleteDouble(double** data)
	{
		delete(*data);
	}

	__declspec(dllexport) void DeleteInt(int** data)
	{
		delete(*data);
	}

	__declspec(dllexport) int add(int a, int b)
	{
		return a + b;
	}
	__declspec(dllexport) int subtract(int a, int b)
	{
		return a - b;
	}
}

//bool clean(
//	const Eigen::MatrixXd & V,
//	const Eigen::MatrixXi & F,
//	Eigen::MatrixXd & CV,
//	Eigen::MatrixXi & CF,
//	Eigen::VectorXi & IM)
//{
//	using namespace igl;
//	using namespace igl::copyleft::tetgen;
//	using namespace igl::copyleft::cgal;
//	using namespace Eigen;
//	using namespace std;
//	//writeOBJ("VF.obj",V,F);
//	const auto & validate_IM = [](
//		const Eigen::MatrixXd & V,
//		const Eigen::MatrixXd & CV,
//		const Eigen::VectorXi & IM)
//	{
//		assert(IM.size() >= CV.rows());
//		for (int i = 0; i < CV.rows(); i++)
//		{
//			if (IM(i) < V.rows() && IM(i) >= 0)
//			{
//				double diff = (V.row(IM(i)) - CV.row(i)).norm();
//				if (diff > 1e-6)
//				{
//					cout << i << ": " << IM(i) << " " << diff << endl;
//				}
//			}
//		}
//	};
//	{
//		MatrixXi _1;
//		VectorXi _2;
//		cout << "clean: remesh_self_intersections" << endl;
//		remesh_self_intersections(V, F, { false,false,false }, CV, CF, _1, _2, IM);
//		for_each(CF.data(), CF.data() + CF.size(), [&IM](int & a) {a = IM(a); });
//		//validate_IM(V,CV,IM);
//		cout << "clean: remove_unreferenced" << endl;
//		{
//			MatrixXi oldCF = CF;
//			unique_simplices(oldCF, CF);
//		}
//		MatrixXd oldCV = CV;
//		MatrixXi oldCF = CF;
//		VectorXi nIM;
//		remove_unreferenced(oldCV, oldCF, CV, CF, nIM);
//		// reindex nIM through IM
//		for_each(IM.data(), IM.data() + IM.size(), [&nIM](int & a) {a = a >= 0 ? nIM(a) : a; });
//		//validate_IM(V,CV,IM);
//	}
//	MatrixXd TV;
//	MatrixXi TT;
//	{
//		MatrixXi _1;
//		// c  convex hull
//		// Y  no boundary steiners
//		// p  polygon input
//		// T1e-16  sometimes helps tetgen
//		cout << "clean: tetrahedralize" << endl;
//		writeOBJ("CVCF.obj", CV, CF);
//		CDTParam params;
//		params.flags = "CYT1e-16";
//		params.use_bounding_box = true;
//		if (cdt(CV, CF, params, TV, TT, _1) != 0)
//		{
//			cout << REDRUM("CDT failed.") << endl;
//			return false;
//		}
//		//writeMESH("TVTT.mesh",TV,TT,MatrixXi());
//	}
//	{
//		MatrixXd BC;
//		barycenter(TV, TT, BC);
//		VectorXd W;
//		cout << "clean: winding_number" << endl;
//		winding_number(V, F, BC, W);
//		W = W.array().abs();
//		const double thresh = 0.5;
//		const int count = (W.array() > thresh).cast<int>().sum();
//		MatrixXi CT(count, TT.cols());
//		int c = 0;
//		for (int t = 0; t < TT.rows(); t++)
//		{
//			if (W(t) > thresh)
//			{
//				CT.row(c++) = TT.row(t);
//			}
//		}
//		assert(c == count);
//		boundary_facets(CT, CF);
//		//writeMESH("CVCTCF.mesh",TV,CT,CF);
//		cout << "clean: remove_unreferenced" << endl;
//		// Force all original vertices to be referenced
//		MatrixXi FF = F;
//		for_each(FF.data(), FF.data() + FF.size(), [&IM](int & a) {a = IM(a); });
//		int ncf = CF.rows();
//		MatrixXi ref(ncf + FF.rows(), 3);
//		ref << CF, FF;
//		VectorXi nIM;
//		remove_unreferenced(TV, ref, CV, CF, nIM);
//		// Only keep boundary faces
//		CF.conservativeResize(ncf, 3);
//		cout << "clean: IM.minCoeff(): " << IM.minCoeff() << endl;
//		// reindex nIM through IM
//		for_each(IM.data(), IM.data() + IM.size(), [&nIM](int & a) {a = a >= 0 ? nIM(a) : a; });
//		cout << "clean: IM.minCoeff(): " << IM.minCoeff() << endl;
//		//validate_IM(V,CV,IM);
//	}
//	return true;
//}

int main(int argc, char *argv[])
{
	using namespace Eigen;
	using namespace std;

	double t1[] = { 90, 115, 10, 90, 135, 10, 90, 115, 0, 90, 115, 0, 90, 135, 10, 90, 135, 0, 90, 115, 10, 110, 115, 10, 110, 135, 10, 90, 135, 10, 90, 115, 10, 110, 135, 10, 90, 115, 0, 110, 115, 0, 110, 115, 10, 90, 115, 10, 90, 115, 0, 110, 115, 10, 90, 135, 0, 110, 135, 0, 90, 115, 0, 90, 115, 0, 110, 135, 0, 110, 115, 0, 90, 135, 10, 110, 135, 10, 90, 135, 0, 90, 135, 0, 110, 135, 10, 110, 135, 0, 110, 115, 0, 110, 135, 0, 110, 135, 10, 110, 115, 10, 110, 115, 0, 110, 135, 10 };
	int t2[] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 };
	double t3[] = { 100, 102, 10, 100, 122, 10, 100, 102, 0, 100, 102, 0, 100, 122, 10, 100, 122, 0, 100, 102, 10, 120, 102, 10, 120, 122, 10, 100, 122, 10, 100, 102, 10, 120, 122, 10, 100, 102, 0, 120, 102, 0, 120, 102, 10, 100, 102, 10, 100, 102, 0, 120, 102, 10, 100, 122, 0, 120, 122, 0, 100, 102, 0, 100, 102, 0, 120, 122, 0, 120, 102, 0, 100, 122, 10, 120, 122, 10, 100, 122, 0, 100, 122, 0, 120, 122, 10, 120, 122, 0, 120, 102, 0, 120, 122, 0, 120, 122, 10, 120, 102, 10, 120, 102, 0, 120, 122, 10 };
	int t4[] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 };
	
	double* pVC = 0;
	int vc = 0;
	int* pFC = 0;
	int fc = 0;
	DoBooleanOperation(t1, 108, t2, 36, t3, 108, t4, 36, 1, &pVC, &vc, &pFC, &fc);

	if (argc != 5)
	{
		cout <<
			"In STL A" << endl <<
			"In STL B" << endl <<
			"Out STL" << endl <<
			"Operation [+,-,&] (union, subtract, intersect)" << endl;

		return -1;
	}

	igl::readSTL(argv[1], VA, FA, NA);
	igl::readSTL(argv[2], VB, FB, NB);

	igl::MeshBooleanType booleanType(igl::MESH_BOOLEAN_TYPE_UNION);
	if (strcmp(argv[4], "-") == 0)
	{
		booleanType = igl::MESH_BOOLEAN_TYPE_MINUS;
	}
	else if (strcmp(argv[4], "&") == 0)
	{
		booleanType = igl::MESH_BOOLEAN_TYPE_INTERSECT;
	}

	igl::copyleft::cgal::mesh_boolean(VA, FA, VB, FB, booleanType, VC, FC, J);
	
	igl::writeSTL(argv[3], VC, FC, true);

	if(false)
	{
		// Plot the mesh with pseudocolors
		igl::opengl::glfw::Viewer viewer;

		// Initialize
		update(viewer);

		viewer.data().show_lines = true;
		viewer.core.camera_dnear = 3.9;
		viewer.launch();
	}
}