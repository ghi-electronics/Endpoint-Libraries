import setuptools



setuptools.setup(
    name = "GHIElectronics.Endpoint.Core",
    version = "0.0.1",
    author = "GHI Electronics",
    author_email = "support@ghielectronics.com",
    license='MIT', 
    description = "GHI Electronics DUE Python library.",
    url = "https://www.duelink.com/",
    packages=setuptools.find_packages(),
    classifiers=[
        "Programming Language :: Python :: 3",        
    ],
 
    python_requires='>=3.6'
)
