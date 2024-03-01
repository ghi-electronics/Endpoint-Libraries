#include <Python.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static PyObject *SpamError;

static PyObject* spam_system1(PyObject* self, PyObject* args)
{
    
    return PyLong_FromLong(123);    
}

static PyMethodDef SpamMethods[] = {
    {"system1", (PyCFunction)spam_system1, METH_VARARGS, "My test method."},
    {NULL, NULL, 0, NULL} /* Sentinel */
};

static struct PyModuleDef spammodule = {
   PyModuleDef_HEAD_INIT,
   "spam",      // name of module
   NULL,  // module documentation, may be NULL
   -1,               // size of per-interpreter state of the module, or -1 if the module keeps state in global variables.
   SpamMethods
};


PyMODINIT_FUNC PyInit_spam() {
    PyObject *m = NULL;
    m = PyModule_Create(&spammodule);

    if (m == NULL)
        return NULL;

    SpamError = PyErr_NewException("spam.error", NULL, NULL);
    Py_XINCREF(SpamError);
    if (PyModule_AddObject(m, "error", SpamError) < 0) {
        Py_XDECREF(SpamError);
        Py_CLEAR(SpamError);
        Py_DECREF(m);
        return NULL;
    }

    return m;
}


