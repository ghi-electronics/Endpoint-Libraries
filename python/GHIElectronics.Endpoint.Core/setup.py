import setuptools



setuptools.setup(
    name = "GHIElectronics.Endpoint.Core",
    version = "0.1.1",
    author = "GHI Electronics",
    author_email = "support@ghielectronics.com",
    license='MIT', 
    description = "GHI Electronics Endpoint Python library.",
    url = "https://www.ghielectronics.com/",
    packages=setuptools.find_packages(),
    classifiers=[
        "Programming Language :: Python :: 3",        
    ],
 
    python_requires='>=3.10'
)
