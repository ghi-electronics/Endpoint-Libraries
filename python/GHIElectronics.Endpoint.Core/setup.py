# The settings are in pyproject.toml. This file only marks the wheel as one
# for the board's processor: it carries libendpoint.so (32-bit ARM, glibc
# 2.34 or newer), which is not a Python extension, so the wheel works with
# every Python 3 version.
from setuptools import setup
from setuptools.command.bdist_wheel import bdist_wheel

PLATFORM = "manylinux_2_34_armv7l"


class BoardWheel(bdist_wheel):
    def finalize_options(self):
        super().finalize_options()
        self.root_is_pure = False

    def get_tag(self):
        return "py3", "none", PLATFORM


setup(cmdclass={"bdist_wheel": BoardWheel})
