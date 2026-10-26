# The settings are in pyproject.toml. This file only marks the wheel as one
# for the board's processor: it carries libendpoint.so (aarch64, glibc 2.34
# or newer), which is not a Python extension, so the wheel works with every
# Python 3 version.
from setuptools import setup
from setuptools.command.bdist_wheel import bdist_wheel

PLATFORM = "manylinux_2_34_aarch64"


class BoardWheel(bdist_wheel):
    def finalize_options(self):
        super().finalize_options()
        self.root_is_pure = False

    def get_tag(self):
        return "py3", "none", PLATFORM


setup(cmdclass={"bdist_wheel": BoardWheel})
