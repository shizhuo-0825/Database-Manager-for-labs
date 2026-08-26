# Database-manager-software-for-labortary
该项目致力于创建一个实验室用数据库管理软件。目前集中于SHG相关数据检索与基本处理，后期有拓展的打算。

## 准备数据库
在数据采集时，需要将`database.csv`文件放在一组原始数据的相同文件夹下，并在其中添加原始数据所对应的实验参数。原始数据文件所在文件夹需要形如`[YYYYMMDD]_[Material]\\[MeasurementMethodsSplittedByUnderlines]\\`实验方法[MeasurementMethodsSplittedByUnderlines]可以任意添加，也可以任意添加子文件夹，各实验方法只需添加下划线作为分隔符。**但是顺序不能混乱，日期与材料必须在方法之前**。若实验数据为.asc文件，则`database.csv`文件需要按照以下方式存储。

`[TimeStamp1].asc`

`[TimeStamp2].asc`

...

`[TimeStamp100].asc`

`database.csv`

随后，该软件可以自动识别实验日期、材料和方法字段。

## 基本原理
该软件会创建9个SQLite数据库以存储数据及数据之间的关系，其中有7个比较完善正在使用。

### DataRecord

（自动生成）独立地记录每一个data的信息。已使用的私有属性为：

1. Id。该Record的主键；
2. DataGroupId。该Record所在Group；
3. RecordName。该Record对应真实原始数据文件的名称（不包括后缀）；
4. RowIndex。该Record所在`database.csv`文件中的行序号；
5. ImageFilePath。该Record对应真实原始数据文件所在路径；
6. ResultQuantity。该Record由软件或本身自带的点数据内容；
7. ResultPath。该Record处理后的高维结果文件所在路径；
8. ExptParams。该Record对应数据参数，以json数据形式记录。

具有链接到DataGroup和AnalysisParams的公共属性。

### DataGroup

（自动生成）记录每一个数据采集后直接出现的自然组，分类依据`database.csv`文件所在文件夹。已使用的私有属性为：

1. Id。该Group的主键；
2. Material。该Group对应的材料名称；
3. ExperimentDate。该Group的时间；
4. CsvPath。该Group对应的`database.csv`的路径；
5. CsvHash。该Group对应的`database.csv`内容的Hash编码以记录最新更改时间；
6. Notes。该Group对应的人工标签；

具有链接到GroupExperimentType的公共属性。

### AnalysisParams

（自动生成）记录每一个Record的处理参数。已使用的私有属性为：

1. Id。该处理参数的主键；
2. HashParamsPath。该处理参数的Hash方式存储的位置；
3. DataRecordId。被处理Record的Id。

具有链接到DataRecord的公共属性。

### GroupExperimentType

（自动生成）记录Group和实验类型之间的关系表。使DataGroup和ExperimentType之间可以多对多。已使用的私有属性为：

1. Id。该类型的主键；
2. DataGroupId。该Group的对应Id；
3. ExperimentTypeId。该Group对应的实验类型Id。

具有链接到DataGroup和ExperimentType的公共属性。


### DataFolder

（自动生成）用户主动打开的某个文件夹。Group和Record将在该文件夹内检索。已使用的私有属性为：

1. Id。该文件夹的主键；
2. FolderPath。该文件夹的路径；
3. ImportedAt。该文件夹的导入时间。

### ExperimentParams

（半自动生成）用户已导入某些数据后自动记录的实验参数表。已使用的私有属性为：

1. Id。该实验参数的主键；
2. FieldName。该实验参数在`database.csv`中的显示名称；
3. DisplayName。该实验参数在绘图或展示时的名称；
4. Unit。该实验参数的单位；
5. DataType。该实验参数的数据存储类型；
6. ExperimentTypeId。该实验参数对应实验类型的Id。

具有链接到ExperimentType的公共属性

### ExperimentType

（手动生成）实验类型表。已使用的私有属性为：

1. Id。该实验方法的主键；
2. Name。该实验方法在文件路径中的名称；
3. DisplayName。该实验方法在显示时的名称；

具有链接到GroupExperimentType的公共属性。

## 使用方法

**用户界面**

基本界面如下：
<img width="1683" height="999" alt="image" src="https://github.com/user-attachments/assets/5fced66a-2d57-4800-83eb-fa0d62715d57" />

其中，上边栏的`Data`, `Process`, `Configs`为有效栏。`Data`栏主要为
