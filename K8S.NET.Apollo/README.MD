![](https://upload-images.jianshu.io/upload_images/2799767-43736dd236959afc.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


# 1.引言

>Apollo（阿波罗）是携程框架部门研发的分布式配置中心，能够集中化管理应用不同环境、不同集群的配置，配置修改后能够实时推送到应用端，并且具备规范的权限、流程治理等特性，适用于微服务配置管理场景。

如官网所述：Apollo 是携程打造的开源配置中心，[GitHub](https://github.com/ctripcorp/apollo)的星星也快点满22K，因此足见它的成熟度和社区活跃度。因此最近在做配置中心选型的时候，经过一番预演，最终敲定Apollo。

Apollo作为微服务体系中必不可少的基础服务，其架构设计和基本使用我们不得不有所了解。

因此本文接下来将主要来介绍如何基于Helm快速部署Apollo集群至K8S，并与.NET Core应用进行集成，同时介绍下如何平滑迁移配置到Apollo。

**本文具有详细的部署步骤，建议动手实操。
部署Chart包和Demo已上传至GitHub，可收藏备用。**

# 2. Apollo 架构一览
在部署之前，需要了解Apollo的基础架构，以便在后续部署工作的展开。

![Apollo 总体设计](https://upload-images.jianshu.io/upload_images/2799767-f467670ed9cee888.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

关于其的解读，我这里就不再详细展开，但以下几点还是要有所了解，感兴趣的可以直接看官网详细介绍：[Apollo配置中心设计](https://github.com/ctripcorp/apollo/wiki/Apollo配置中心设计)。

1. Config Service提供配置的读取、推送等功能，服务对象是Apollo客户端
2. Admin Service提供配置的修改、发布等功能，服务对象是Apollo Portal（管理界面）
3. Config Service和Admin Service都是多实例、无状态部署，需要通过注册中心进行服务注册和发现
4. 注册中心默认采用的是Eureka，在K8S中由Service充当
5. Apollo客户端通过注册中心获取Config Service服务列表进行配置读取
6. Apollo Portal通过注册中心获取Admin Service服务列表进行配置管理

基于上面对Apollo的介绍，其物理架构总结起来就是：
1. 每一套环境都必须拥有自己独立的Config Service 和 Admin Service 以及独立ConfigDB。
2. 多套环境可以公用一套Apollo Portal 进行管理，Portal拥有独立PortalDB。


# 3. 基于Helm部署到K8S
因为Apollo 1.7.0版本增加了基于Kubernetes原生服务发现的部署模式，来替换内置的Eureka，所以在整体部署上有很大简化，同时官方也提供了Helm Charts，让Apollo更加易于开箱即用。下面就以部署一套测试环境为例讲解一下Apollo的部署要点。（部署至本机Docker Desktop Local K8S环境）。

*环境要求：  Kubernetes 1.10+，Helm 3*

![](https://upload-images.jianshu.io/upload_images/2799767-e92736ebe551b07b.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


## 3.1 搭建 Apollo Config&Portal DB
从上图的物理架构上来看，首先要部署好Config DB和PortalDB。关于DB的搭建，建议直接使用`bitnami/mysql`chart搭建。搭建步骤如下：
```
> helm repo add bitnami https://charts.bitnami.com/bitnami
> helm repo list
> helm repo update
> helm search repo bitnami/mysql
NAME            CHART VERSION   APP VERSION     DESCRIPTION
bitnami/mysql   6.14.8          8.0.21          Chart to create a Highly available MySQL cluster
```

执行helm包的安装，需要自定义配置文件，也就是`values.yaml`。我们可以先行下载 mysql chart包。

> 之所以选择将chart包下载到本地，是为了确保后续维护能够基于一致的chart包版本。避免因为执行`helm repo update`导致chart包版本自动升级，而不自知。

```
> helm pull bitnami/mysql --untar  //下载并解包
mysql
 ├── Chart.yaml
 ├── ci
 │   └── values-production.yaml
 ├── files
 │   └── docker-entrypoint-initdb.d
 │       └── README.md
 ├── README.md
 ├── templates
 │   ├── initialization-configmap.yaml
 │   ├── master-configmap.yaml
 │   ├── master-statefulset.yaml
 │   ├── master-svc.yaml
 │   ├── NOTES.txt
 │   ├── secrets.yaml
 │   ├── serviceaccount.yaml
 │   ├── servicemonitor.yaml
 │   ├── slave-configmap.yaml
 │   ├── slave-statefulset.yaml
 │   ├── slave-svc.yaml
 │   └── _helpers.tpl
 ├── values-production.yaml
 └── values.yaml
```

根据官网[分布式部署指南](https://github.com/ctripcorp/apollo/wiki/分布式部署指南)中所示，其提供了DB的初始化脚本用来分别创建`ApolloConfigDB`和`ApolloPortalDB`。因此可以直接将以上SQL脚本下载到mysql chart的`files/docker-entrypoint-initdb.d`目录下，这样在部署mysql实例时就会自动执行脚本创建数据库。

```
> cd mysql/files/docker-entrypoint-initdb.d
> curl https://raw.githubusercontent.com/ctripcorp/apollo/master/scripts/sql/apolloportaldb.sql > apolloportaldb.sql //下载apolloportaldb.sql
> curl https://raw.githubusercontent.com/ctripcorp/apollo/master/scripts/sql/apolloconfigdb.sql > apolloconfigdb.sql 下载apolloconfigdb.sql
> ls

    Directory: C:\Users\Shengjie\k8s\helm\charts\apollo\mysql\files\docker-entrypoint-initdb.d

Mode                 LastWriteTime         Length Name
----                 -------------         ------ ----
-a---           8/12/2020 11:01 PM          21291 apolloconfigdb.sql
-a---           8/12/2020 10:56 PM          16278 apolloportaldb.sql
-a---            8/9/2020  6:26 PM            242 README.md
```

然后复制`values.yaml`并命名为`dev-mysql-values.yaml`。然后修改核心配置：
1. global.storageClass=hostpath
可通过`kubectl get sc`查看集群支持的storageClass，我这边选择默认的hostpath。其创建的pv的默认回收策略为delete，也就意味着卸载mysql，数据直接删除，这点需要注意！！！如果需要保留测试数据，请更新storageClass。
2. root.password=root
修改默认root用户的密码

修改完毕后，执行以下脚本进行安装：
```
> kubectl create ns db #创建单独db命名空间
> helm install mysql-apollo . -f dev-mysql-values.yaml -n db
NAME: mysql-apollo
LAST DEPLOYED: Sun Aug 16 11:01:18 2020
NAMESPACE: db
STATUS: deployed
REVISION: 1
TEST SUITE: None
NOTES:
Please be patient while the chart is being deployed

Tip:

  Watch the deployment status using the command: kubectl get pods -w --namespace db

Services:

  echo Master: mysql-apollo.db.svc.cluster.local:3306
  echo Slave:  mysql-apollo-slave.db.svc.cluster.local:3306

Administrator credentials:

  echo Username: root
  echo Password : $(kubectl get secret --namespace db mysql-apollo -o jsonpath="{.data.mysql-root-password}" | base64 --decode)

To connect to your database:

  1. Run a pod that you can use as a client:

      kubectl run mysql-apollo-client --rm --tty -i --restart='Never' --image  docker.io/bitnami/mysql:8.0.21-debian-10-r17 --namespace db --command -- bash

  2. To connect to master service (read/write):

      mysql -h mysql-apollo.db.svc.cluster.local -uroot -p my_database

  3. To connect to slave service (read-only):

      mysql -h mysql-apollo-slave.db.svc.cluster.local -uroot -p my_database

To upgrade this helm chart:

  1. Obtain the password as described on the 'Administrator credentials' section and set the 'root.password' parameter as shown below:

      ROOT_PASSWORD=$(kubectl get secret --namespace db mysql-apollo -o jsonpath="{.data.mysql-root-password}" | base64 --decode)
      helm upgrade mysql-apollo bitnami/mysql --set root.password=$ROOT_PASSWORD

```

按照上面提示，验证数据库成功创建：
```
> kubectl run mysql-apollo-client --rm --tty -i --restart='Never' --image  docker.io/bitnami/mysql:8.0.21-debian-10-r17 
--namespace db --command -- bash  # 创建mysql-client pod
I have no name!@mysql-apollo-client:/$ mysql -h mysql-apollo.db.svc.cluster.local -uroot -proot    # 连接至master 节点    
mysql: [Warning] Using a password on the command line interface can be insecure.
Welcome to the MySQL monitor.  Commands end with ; or \g.
Your MySQL connection id is 61
Server version: 8.0.21 Source distribution

Copyright (c) 2000, 2020, Oracle and/or its affiliates. All rights reserved.

Oracle is a registered trademark of Oracle Corporation and/or its
affiliates. Other names may be trademarks of their respective
owners.

Type 'help;' or '\h' for help. Type '\c' to clear the current input statement.

mysql> show databases; # 查看databases;
+--------------------+
| Database           |
+--------------------+
| ApolloConfigDB     |
| ApolloPortalDB     |
| information_schema |
| my_database        |
| mysql              |
| performance_schema |
| sys                |
+--------------------+
7 rows in set (0.00 sec)

mysql> use ApolloConfigDB; # 切换至ApolloConfigDB；
Reading table information for completion of table and column names
You can turn off this feature to get a quicker startup with -A

Database changed
mysql> show tables;  # 查看数据表；
+--------------------------+
| Tables_in_ApolloConfigDB |
+--------------------------+
| AccessKey                |
| App                      |
| AppNamespace             |
| Audit                    |
| Cluster                  |
| Commit                   |
| GrayReleaseRule          |
| Instance                 |
| InstanceConfig           |
| Item                     |
| Namespace                |
| NamespaceLock            |
| Release                  |
| ReleaseHistory           |
| ReleaseMessage           |
| ServerConfig             |
+--------------------------+
16 rows in set (0.01 sec)
```

至此，确认Apollo ConfigDB和PortalDB搭建成功。

## 3.2 搭建 Apollo Config Service 
搭建Apollo Service 需要添加携程官方chart仓库：
```
> helm repo add apollo http://ctripcorp.github.io/apollo/charts
> helm search repo apollo
NAME                    CHART VERSION   APP VERSION     DESCRIPTION
apollo/apollo-portal    0.1.0           1.7.0           A Helm chart for Apollo Portal
apollo/apollo-service   0.1.0           1.7.0           A Helm chart for Apollo Config Service and Apol...
```
从上可知，主要包含两个chart，分别用来部署service和portal。下来研究下apollo/apollo-service 这个chart。老规矩，先把chart包下载下来：
```
> helm pull apollo/apollo-service --untar
apollo-service
 ├── Chart.yaml
 ├── templates
 │   ├── deployment-adminservice.yaml
 │   ├── deployment-configservice.yaml
 │   ├── NOTES.txt
 │   ├── service-adminservice.yaml
 │   ├── service-configdb.yaml
 │   ├── service-configservice.yaml
 │   └── _helpers.tpl
 └── values.yaml
```

从上面的树形图来看，主要就是用来部署config service 和 admin service。紧接着，复制一个`values.yaml`，命名为`dev-apollo-svc-values.yaml`。主要修改以下配置：
1. configdb.host=mysql-apollo.db
指定configdb的主机，因为是在集群内部，直接使用服务名即可
2. configdb.password=root
指定configdb的秘密

修改后的配置如下：
```
configdb:
  name: apollo-configdb
  # apolloconfigdb host
  host: "mysql-apollo.db"
  port: 3306
  dbName: ApolloConfigDB
  # apolloconfigdb user name
  userName: "root"
  # apolloconfigdb password
  password: "root"
....
```
其他配置可以暂定不动，紧接着执行以下命令进行安装：
```
> kubectl create ns apollo # 创建apollo 命名空间
> helm install --dry-run --debug apollo-dev-svc . -f dev-apollo-svc-values.yaml -n apollo # 测试安装，验证模板生成的资源文件是否有误
> helm install apollo-dev-svc . -f dev-apollo-svc-values.yaml -n apollo
NAME: apollo-dev-svc
LAST DEPLOYED: Sun Aug 16 11:17:38 2020
NAMESPACE: apollo
STATUS: deployed
REVISION: 1
TEST SUITE: None
NOTES:
Get meta service url for current release by running these commands:
  echo http://apollo-dev-svc-apollo-configservice.apollo:8080      

For local test use:
  export POD_NAME=$(kubectl get pods --namespace apollo -l "app=apollo-dev-svc-apollo-configservice" -o jsonpath="{.items[0].metadata.name}")
  echo http://127.0.0.1:8080
  kubectl --namespace apollo port-forward $POD_NAME 8080:8080
```
这里要记住上面的meta service url：`http://apollo-dev-svc-apollo-configservice.apollo:8080`

那如何确认正确部署了呢：
```
> kubectl get all -n apollo # 查看apollo命名空间下部署的资源
NAME                                                       READY   STATUS    RESTARTS   AGE
pod/apollo-dev-svc-apollo-adminservice-7d4468ff46-gw6h4    1/1     Running   0          3m26s
pod/apollo-dev-svc-apollo-configservice-58d6c44cd4-n4qk9   1/1     Running   0          3m26s

NAME                                          TYPE        CLUSTER-IP       EXTERNAL-IP   PORT(S)    AGE
service/apollo-dev-svc-apollo-adminservice    ClusterIP   10.99.251.14     <none>        8090/TCP   3m26s
service/apollo-dev-svc-apollo-configservice   ClusterIP   10.108.121.201   <none>        8080/TCP   3m26s

NAME                                                  READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/apollo-dev-svc-apollo-adminservice    1/1     1            1           3m26s
deployment.apps/apollo-dev-svc-apollo-configservice   1/1     1            1           3m26s

NAME                                                             DESIRED   CURRENT   READY   AGE
replicaset.apps/apollo-dev-svc-apollo-adminservice-7d4468ff46    1         1         1       3m26s
replicaset.apps/apollo-dev-svc-apollo-configservice-58d6c44cd4   1         1         1       3m26s

```
从上可知暴露了两个服务configservice和adminservice，来尝试将configservice进行端口转发到本地端口来看一下。
```
> kubectl port-forward service/apollo-dev-svc-apollo-configservice 8080:8080 -n apollo # 转发configservice到本地服务
Forwarding from 127.0.0.1:8080 -> 8080
Forwarding from [::1]:8080 -> 8080
```
使用浏览器访问 [localhost:8080](http://localhost:8080)，可以看到输出`[{"appName":"apollo-configservice","instanceId":"apollo-configservice:http://apollo.shisheng.wang/config-svc","homepageUrl":"http://apollo.shisheng.wang/config-svc"},{"appName":"apollo-adminservice","instanceId":"apollo-adminservice:http://apollo.shisheng.wang/admin-svc","homepageUrl":"http://apollo.shisheng.wang/admin-svc"}]`。

至此说明，Apollo Service 搭建成功。


## 3.3 搭建 Apollo Portal Service
同样，先来下载portal chart包，并研究下目录结构：
```
> helm pull apollo/apollo-portal --untar
apollo-portal
 ├── Chart.yaml
 ├── templates
 │   ├── deployment-portal.yaml
 │   ├── ingress-portal.yaml
 │   ├── NOTES.txt
 │   ├── service-portal.yaml
 │   ├── service-portaldb.yaml
 │   └── _helpers.tpl
 └── values.yaml
```
从上可知，portal 相对来说，主要是构建portal服务，并可以通过ingress暴露服务。复制一个`values.yaml`，命名为`dev-apollo-portal-values.yaml`。主要修改以下配置：
1. `ingress.enabled=true`
启用ingress，并通过注解设置ingress controller，因为portal是个有状态服务，所以要关注Sessiion状态维持。以下主要是针对nginx-ingress-controller的配置，如果使用的其他的ingress-controller请注意更改。（nginx-ingress-controller的安装，这里就不具体展开了，可以简单执行`helm install nginx bitnaim/nginx-ingress-controller` 安装就好了。）
```
ingress:
  enabled: true
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/rewrite-target: /
    nginx.ingress.kubernetes.io/affinity: "cookie"
    nginx.ingress.kubernetes.io/session-cookie-name: "route"
  hosts:
    - host: "apollo.demo.com"
      paths: ["/"]
  tls: []
```
2. 指定配置源 ，主要是envs和metaServers两个配置项：
`config.envs=dev`
`config.metaServers.dev=http://apollo-dev-svc-apollo-configservice.apollo:8080`（上面部署apollo service输出的apollo service url）*如果同时启用开发、测试和生产环境。可以配置为：`envs: "dev,uat,prd"`，metaServers 分别指定对应环境的配置即可。*
以下是只启用开发环境的配置：
```
config:
  # spring profiles to activate
  profiles: "github,auth"
  # specify the env names, e.g. dev,pro
  envs: "dev"
  # specify the meta servers, e.g.
  # dev: http://apollo-configservice-dev:8080
  # pro: http://apollo-configservice-pro:8080
  metaServers: 
    dev: http://apollo-svc-dev-apollo-configservice.apollo:8080
    # dev: http://apollo.shisheng.wang
  # specify the context path, e.g. /apollo
  contextPath: ""
  # extra config files for apollo-portal, e.g. application-ldap.yml
  files: {}
```
3. portaldb.host=mysql-apollo.db & portaldb.password=root
指定portaldb的主机和密码
```
portaldb:
  name: apollo-portaldb
  # apolloportaldb host
  host: mysql-apollo.db
  port: 3306
  dbName: ApolloPortalDB
  # apolloportaldb user name
  userName: root
  # apolloportaldb password
  password: root
```
其他配置可以暂定不动，紧接着执行以下命令进行安装：
```
> Helm install --dry-run --debug apollo-dev-portal . -f dev-apollo-portal-values.yaml -n apollo # 测试安装，验证模板生成的资源文件是否有误
> Helm install apollo-dev-portal . -f dev-apollo-portal-values.yaml -n apollo
PS C:\Users\Shengjie\k8s\helm\charts\apollo\apollo-portal> Helm install apollo-dev-portal . -f dev-apollo-portal-values.yaml -n apollo
NAME: apollo-dev-portal
LAST DEPLOYED: Sun Aug 16 11:53:18 2020
NAMESPACE: apollo
STATUS: deployed
REVISION: 1
TEST SUITE: None
NOTES:
Get apollo portal url by running these commands:
  http://apollo.demo.com/
```
到这一步，如果需要本地可以访问，还需要修改本地hosts，添加`127.0.0.1 apollo.demo.com`。然后打开你的Browser输入[http://apollo.demo.com/](http://apollo.demo.com/)，就可以访问了。默认用户密码是：[apollo/admin]。
![apollo login page](https://upload-images.jianshu.io/upload_images/2799767-021717d0f5f8392c.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

# 3.4. 暴露 config service
以上部署的是开发环境，但要想开发环境要访问到config service，我们还需要些小动作。这个时候就需要修改apollo service的chart模板，在`template`目录增加`ingress-configservice.yaml`文件，内容如下：
```
# ingress-configservice.yaml
{{- if .Values.configService.ingress.enabled -}}
{{- $fullName := include "apollo.configService.fullName" . -}}
{{- $svcPort := .Values.configService.service.port -}}
{{- if semverCompare ">=1.14-0" .Capabilities.KubeVersion.GitVersion -}}
apiVersion: networking.k8s.io/v1beta1
{{- else -}}
apiVersion: extensions/v1beta1
{{- end }}
kind: Ingress
metadata:
  name: {{ $fullName }}
  labels:
    {{- include "apollo.service.labels" . | nindent 4 }}
  {{- with .Values.configService.ingress.annotations }}
  annotations:
    {{- toYaml . | nindent 4 }}
  {{- end }}
spec:
{{- if .Values.configService.ingress.tls }}
  tls:
  {{- range .Values.configService.ingress.tls }}
    - hosts:
      {{- range .hosts }}
        - {{ . | quote }}
      {{- end }}
      secretName: {{ .secretName }}
  {{- end }}
{{- end }}
  rules:
  {{- range .Values.configService.ingress.hosts }}
    - host: {{ .host | quote }}
      http:
        paths:
        {{- range .paths }}
          - path: {{ . }}
            backend:
              serviceName: {{ $fullName }}
              servicePort: {{ $svcPort }}
        {{- end }}
  {{- end }}
{{- end }}
```
然后修改`values.yaml`在`configService`节点下增加`ingress`配置选项：
```
configService:
  name: apollo-configservice
  fullNameOverride: ""
  replicaCount: 2
  containerPort: 8080
  image:
    repository: apolloconfig/apollo-configservice
    pullPolicy: IfNotPresent
  imagePullSecrets: []
  service:
    fullNameOverride: ""
    port: 8080
    targetPort: 8080
    type: ClusterIP
  # 以下为新增ingress配置项  
  ingress:
    enabled: false
    annotations: {}
    hosts:
      - host: ""
        paths: []
    tls: []
```

然后再修改上面我们创建的`dev-apollo-svc-values.yaml`下的`configService`节点，添加对应`ingress`和`config.configServiceUrlOverride`配置：
```
configService:
  name: apollo-configservice
  fullNameOverride: ""
  replicaCount: 1
  containerPort: 8080
  image:
    repository: apolloconfig/apollo-configservice
    pullPolicy: IfNotPresent
  imagePullSecrets: []
  service:
    fullNameOverride: ""
    port: 8080
    targetPort: 8080
    type: ClusterIP
  ingress:
    enabled: true
    annotations:
      kubernetes.io/ingress.class: nginx
      nginx.ingress.kubernetes.io/rewrite-target: /$2
    hosts:
      - host: "apollo.demo.com"
        paths: ["/config-svc(/|$)(.*)"]
    tls: []
  liveness:
    initialDelaySeconds: 100
    periodSeconds: 10
  readiness:
    initialDelaySeconds: 30
    periodSeconds: 5
  config:
    # spring profiles to activate
    profiles: "github,kubernetes"
    # override apollo.config-service.url: config service url to be accessed by apollo-client
    configServiceUrlOverride: "http://apollo.demo.com/config-svc"
    # override apollo.admin-service.url: admin service url to be accessed by apollo-portal
    adminServiceUrlOverride: ""

```

修改完毕，执行以下命令升级apollo service：
```
> helm upgrade apollo-service-dev . -f dev-apollo-svc-values.yaml -n apollo
NAME: apollo-service-dev
LAST DEPLOYED: Tue Aug 18 14:20:41 2020
NAMESPACE: apollo
STATUS: deployed
REVISION: 1
TEST SUITE: None
NOTES:
Get meta service url for current release by running these commands:
  echo http://apollo-service-dev-apollo-configservice.apollo:8080

For local test use:
  export POD_NAME=$(kubectl get pods --namespace apollo -l "app=apollo-service-dev-apollo-configservice" -o jsonpath="{.items[0].metadata.name}")
  echo http://127.0.0.1:8080
  kubectl --namespace apollo port-forward $POD_NAME 8080:8080
> curl http://apollo.demo.com/config-svc
[{"appName":"apollo-configservice","instanceId":"apollo-configservice:http://apollo.demo.com/config-svc","homepageUrl":"http://apollo.demo.com/config-svc"},{"appName":"apollo-adminservice","instanceId":"apollo-adminservice:http://apollo-service-dev-apollo-adminservice.apollo:8090","homepageUrl":"http://apollo-service-dev-apollo-adminservice.apollo:8090"}]
```
从上面的输出可以看到，现在已经可以通过`http://apollo.demo.com/config-svc`读取metaServer配置了，后面本地开发环境就可以通过这个链接来读取Apollo的配置。


# 4. .NET Core 集成Apollo
这一部分我就快速带过了，执行以下命令创建项目，并引入`apollo`和`swagger`相关包：
```
> dotnet new webapi -n K8S.NET.Apollo
> cd K8S.NET.Apollo
> dotnet add package Com.Ctrip.Framework.Apollo.Configuration
> dotnet add package Swashbuckle.AspNetCore

```
修改`appsettings.json`增加`apollo`配置：
```
{    
    "AllowedHosts": "*",
    "apollo": {
        "AppId": "test",
        "MetaServer": "http://apollo.demo.com/config-svc",
        "Env": "Dev"
    }    
}
```
修改`Program.cs`，添加Apollo配置源如下：
```
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configBuilder =>
    {
        configBuilder.AddApollo(configBuilder.Build().GetSection("apollo"))
            .AddDefault()
            .AddNamespace("TEST1.connectionstrings", "ConnectionStrings")
            .AddNamespace("logging", ConfigFileFormat.Json)
            ;
    })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```
修改`Startup.cs`，添加Swagger集成，方便测试：
```
public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = this.GetType().Namespace, Version = "v1" });
    });
}
// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{this.GetType().Namespace} V1");
        c.RoutePrefix = string.Empty;
    });

    //...
}
```

添加`ApolloController`，增加以下测试代码：
```
namespace K8S.NET.Apollo.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ApolloController : Controller
    {
        private readonly IConfiguration _configuration;
        public ApolloController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("key")]
        public IActionResult GetLogLevelSection()
        {
            var key = "Logging:LogLevel";
            var val = _configuration.GetSection(key).Get<LoggingOptions>();
            return Ok($"{key}:{JsonSerializer.Serialize(val)}");
        }

        [HttpGet("key")]
        public IActionResult GetString(string key)
        {
            var val = _configuration.GetValue<string>(key);
            return Ok($"{key}:{val}");
        }

        [HttpGet("key")]
        public IActionResult GetConnectionStrings(string key)
        {
            var val = _configuration.GetConnectionString(key);
            return Ok($"{key}:{val}");
        }
    }

    public class LoggingOptions : Dictionary<string, string>
    {
    }
}
```
登录Apollo Portal，添加test项目，并增加以下配置，并发布。
![增加配置](https://upload-images.jianshu.io/upload_images/2799767-26742654e9fa45fc.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

本地调试，就能够获取云端配置，另外Apollo同时会同步一份配置到本地目录：`c:/opt/data/test/config-cache`。这样就可以保证即使无法建立云端连接，也可以正常加载本地配置。
执行以下命令，进行配置读取和验证：
```
> curl https://localhost:5001/Apollo/GetLogLevelSection
Logging:LogLevel:{"Default":"Information","Microsoft":"Warning","Microsoft.Hosting.Lifetime":"Information"}
> curl https://localhost:5001/Apollo/GetString/key?key=name
name:Shengjie
> curl https://localhost:5001/Apollo/GetConnectionStrings/key?key=Default
Default:Server=mu3ne-mysql;port=3306;database=mu3ne0001;user id=root;password=abc123;AllowLoadLocalInfile=true
```





# 5.配置迁移指北
相信采用Apollo的绝大多数都不是一开始就用的，都是再配置逐渐复杂之后，才进行迁移的。我也不例外，之前是用K8S的ConfigMap来做配置管理。下面就来讲下迁移指南，我将其分为两种模式：
1. 偷懒模式
如果想改动最小，就直接将项目配置继续以Json格式维护到Apollo的私有命名空间下。
![](https://upload-images.jianshu.io/upload_images/2799767-cb5f7cdd209df803.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)
![](https://upload-images.jianshu.io/upload_images/2799767-365c7ec8ea0774fa.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

```
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, builder) =>
        {
            builder.AddApollo(builder.Build().GetSection("apollo"))
                .AddDefault()
                .AddNamespace("appsettings",ConfigFileFormat.Json);
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```
2. 强迫症模式
也有人考虑，既然上Apollo，就要用到它的特性，因此对现有配置就要分门别类。哪些是公用的，哪些是私有的。对于公用的就要定义到公共的命名空间下。公共命名空间的配置格式只有Properties格式，因此需要将Json转为Properties。比如针对`Logging`配置可以借助网站 [json2properties converter](https://tools.fromdev.com/json-to-property-converter.html)进行在线转换。如下所示：


![json2properties](https://upload-images.jianshu.io/upload_images/2799767-145fd931518f0f42.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

如果真这样做，你就错了，你会发现最终的日志配置不生效。这是因为`properties`格式是以**`.`**进行分割，而.NET Core是用**`:`**来识别节点配置， 因此`properties`配置按**`:`**分割就好了，如下所示，以下两种配置等效：

![json 与 properties 相互转换](https://upload-images.jianshu.io/upload_images/2799767-bd49e8486057cdd0.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


# 6. 最后
以上，相信若能够动手实操，你将收获匪浅。

本文Demo和Chart包的完整配置已上传至Github，请按需取用。